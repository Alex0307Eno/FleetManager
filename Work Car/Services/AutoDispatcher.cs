using Cars.Data;
using Cars.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Cars.Services
{
    public class AutoDispatcher
    {
        private readonly ApplicationDbContext _db;

        public AutoDispatcher(ApplicationDbContext db)
        {
            _db = db;
        }

        public sealed class DispatchResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public int? DriverId { get; set; }
            public int? VehicleId { get; set; }
            public string? DriverName { get; set; }
            public string? PlateNo { get; set; }
        }

        // 時段交集：a 與 b 有重疊 => aStart < bEnd && bStart < aEnd
        private static bool Overlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
            => aStart < bEnd && bStart < aEnd;

        // 主要對外入口：依日期/時間/人數，自動選班表司機 + 可用車
        public async Task<DispatchResult> AssignAsync(
            int carApplyId,
            DateTime useStart,
            DateTime useEnd,
            int passengerCount,
            string? vehicleType = null)
        {
            // 以台北時區計算，可依你專案改成 IConfiguration 調整
            var localStart = useStart;
            var localEnd = useEnd;
            var day = localStart.Date;
            var isWeekday = localStart.DayOfWeek >= DayOfWeek.Monday && localStart.DayOfWeek <= DayOfWeek.Friday;

            // 根據開始時間推斷「第一優先」時段
            var chain = new List<string>();
            if (isWeekday)
            {
                if (localStart.TimeOfDay < TimeSpan.FromHours(12)) chain.Add("AM"); else chain.Add("PM");
            }
            // 平日與假日皆可往一般班遞補
            chain.AddRange(new[] { "G1", "G2", "G3" });

            using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // 1) 先找當天符合 chain 的班表，照 chain 順序挑第一個
                var schedule = (await _db.Schedules
                .Where(s => s.WorkDate == day && chain.Contains(s.Shift))
                .AsNoTracking()
                .ToListAsync())  // ✅ 先拉到記憶體
                .OrderBy(s => chain.IndexOf(s.Shift))
                .FirstOrDefault();


                if (schedule == null)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = $"當日({day:yyyy-MM-dd})班表找不到可派的人員（Shift: {string.Join("/", chain)}）"
                    };
                }

                // 2) 檢查該司機是否有時間衝突
                var driverBusy = await _db.Dispatches
                .AnyAsync(o => o.DriverId == schedule.DriverId &&
                   useStart < o.EndTime &&
                   o.StartTime < useEnd);



                if (driverBusy)
                {
                    // 如果該司機衝突 → 依 chain 換下一個班的司機（例如 AM 改 G1，再來 G2/G3）
                    var alt = (await _db.Schedules
                    .Where(s => s.WorkDate == day && chain.Contains(s.Shift) && s.DriverId != schedule.DriverId)
                    .AsNoTracking()
                    .ToListAsync())   
                    .OrderBy(s => chain.IndexOf(s.Shift))
                    .ToList();

                    schedule = null;
                    foreach (var s in alt)
                    {
                        var busy = await _db.Dispatches
                         .AnyAsync(o => o.DriverId == s.DriverId &&
                        useStart < o.EndTime &&
                        o.StartTime < useEnd);



                        if (!busy)
                        {
                            schedule = s;
                            break;
                        }
                    }

                    if (schedule == null)
                    {
                        return new DispatchResult
                        {
                            Success = false,
                            Message = $"當日({day:yyyy-MM-dd})所有備援班次皆已衝突（{string.Join("/", chain)}）。"
                        };
                    }
                }

                // 3) 找可用車：狀態=可用、容量足夠、時間不衝突；可依 vehicleType 再加條件
                var vehiclesQuery = _db.Vehicles
                    .Where(v => v.Status == "可用" && (passengerCount <= 0 || (v.Capacity == null || v.Capacity >= passengerCount)));

                

                var allVehicles = await vehiclesQuery.AsNoTracking().ToListAsync();
                int? chosenVehicleId = null;
                string? chosenPlate = null;

                foreach (var v in allVehicles)
                {
                    var used = await _db.Dispatches
                    .AnyAsync(o => o.VehicleId == v.VehicleId &&
                   useStart < o.EndTime &&
                   o.StartTime < useEnd);

                    if (!used)
                    {
                        chosenVehicleId = v.VehicleId;
                        chosenPlate = v.PlateNo;
                        break;
                    }
                }

                if (chosenVehicleId == null)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "沒有符合時段/容量的可用車輛。"
                    };
                }

                // 4) 建立派工單（也可以只把 DriverId/VehicleId 回寫到 CarApplications 內）
                var dispatch = new Cars.Models.Dispatch
                {
                    ApplyId = carApplyId,
                    DriverId = schedule.DriverId,
                    VehicleId = chosenVehicleId.Value,
                    StartTime = useStart,
                    EndTime = useEnd,
                    CreatedAt = DateTime.UtcNow,
                    DispatchStatus = "待派車"
                };

                _db.Dispatches.Add(dispatch);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                // 查司機名字
                var driverName = await _db.Drivers
                    .Where(d => d.DriverId == schedule.DriverId)
                    .Select(d => d.DriverName)
                    .FirstAsync();


                return new DispatchResult
                {
                    Success = true,
                    Message = "派工成功",
                    DriverId = schedule.DriverId,
                    VehicleId = chosenVehicleId,
                    DriverName = driverName,
                    PlateNo = chosenPlate
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new DispatchResult
                {
                    Success = false,
                    Message = $"派工例外：{ex.Message} ({ex.InnerException?.Message})"
                };
            }
        }
        // 找出在 shift 這段期間「有班表」的一位駕駛（可加上車種/乘客數等條件）
        public async Task<int?> FindOnDutyDriverIdAsync(DateTime useStart, DateTime useEnd)
        {
            var day = useStart.Date;

            
            List<string> BuildShiftChain(DateTime t)
            {
                var hhmm = t.TimeOfDay;
                if (hhmm < TimeSpan.FromHours(11.5)) return new() { "AM", "G1", "G2", "G3" };
                if (hhmm < TimeSpan.FromHours(13.5)) return new() { "PM", "G1", "G2", "G3" };
                if (hhmm < TimeSpan.FromHours(17)) return new() { "PM", "G2", "G3", "G1" };
                return new() { "PM", "G3", "G2", "G1" };
            }

            var chain = BuildShiftChain(useStart);

            // 抓當日符合優先順序的班表，依 chain 排序
            var daySchedules = (await _db.Schedules
                .Where(s => s.WorkDate == day && chain.Contains(s.Shift))
                .AsNoTracking()
                .ToListAsync())
                .OrderBy(s => chain.IndexOf(s.Shift))
                .ToList();

            foreach (var s in daySchedules)
            {
                // 司機在該時段不可已有派遣（避免衝突）
                var busy = await _db.Dispatches.AnyAsync(d =>
                    d.DriverId == s.DriverId &&
                    useStart < d.EndTime &&
                    d.StartTime < useEnd);

                if (!busy)
                    return s.DriverId;
            }

            return null; // 找不到可用駕駛就回傳 null
        }
    }

   
}
