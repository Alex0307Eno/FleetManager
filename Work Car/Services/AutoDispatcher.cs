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


        private static List<string> BuildShiftChain(DateTime start, bool isLongTrip)
        {
            var t = start.TimeOfDay;
            bool early = t < TimeSpan.FromHours(11.5);
            bool aft = t >= TimeSpan.FromHours(13.5) && t < TimeSpan.FromHours(17);

            var chain = new List<string>();
            if (isLongTrip) chain.Add("G3");                  // ★ 長差第一順位

            if (early) chain.AddRange(new[] { "AM", "G1" });
            else if (aft) chain.AddRange(new[] { "PM", "G2" });
            else chain.AddRange(new[] { "AM", "PM", "G1", "G2" });

            // 備援
            chain.AddRange(new[] { "AM", "PM", "G1", "G2", "G3" });
            return chain.Distinct().ToList();
        }

        /// 派工選項
        public sealed class AssignOptions
        {
            /// 距離（公里），>30 視為長差
            public double? DistanceKm { get; set; }

            /// 是否只派駕駛（預設 true：用車申請階段不自動派車）
            public bool DriverOnly { get; set; } = true;

            /// 管理員可指定車輛（審核階段用，可選）
            public int? PreferredVehicleId { get; set; }
        }

        // 舊簽名保留，讓既有呼叫不用改碼
        public Task<DispatchResult> AssignAsync(
            int carApplyId,
            DateTime useStart,
            DateTime useEnd,
            int passengerCount,
            string? vehicleType = null)
        {
            // 依你的規則：預設只派駕駛，車輛等管理員審核再派
            return AssignAsync(
                carApplyId, useStart, useEnd, passengerCount, vehicleType,
                new AssignOptions { DriverOnly = true }
            );
        }

        // 主要對外入口：依日期/時間/人數，自動選班表司機 + 可用車
        public async Task<DispatchResult> AssignAsync(
    int carApplyId,
    DateTime useStart,
    DateTime useEnd,
    int passengerCount,
    string? vehicleType,
    AssignOptions? options)
        {
            options ??= new AssignOptions();

            var localStart = useStart;
            var localEnd = useEnd;
            var day = localStart.Date;
            var reasons = new List<string>();

            // === 窗口定義 ===
            var T0730 = TimeSpan.FromHours(7.5);   // 07:30
            var T1130 = TimeSpan.FromHours(11.5);  // 11:30
            var T1330 = TimeSpan.FromHours(13.5);  // 13:30
            var T1700 = TimeSpan.FromHours(17);    // 17:00

            bool OverlapWin(DateTime s, DateTime e, TimeSpan ws, TimeSpan we)
                => s.TimeOfDay < we && ws < e.TimeOfDay;

            bool inEarlyWindow = OverlapWin(localStart, localEnd, T0730, T1130);  // 07:30–11:30
            bool inAfternoonWindow = OverlapWin(localStart, localEnd, T1330, T1700);  // 13:30–17:00

            // === 判斷長差 ===
            var apply = await _db.CarApplications
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ApplyId == carApplyId);

            bool isLongTrip = false;
            if (apply != null)
            {
                double km = 0;
                if (!string.IsNullOrEmpty(apply.RoundTripDistance))
                    double.TryParse(apply.RoundTripDistance.Replace(" 公里", ""), out km);
                else if (!string.IsNullOrEmpty(apply.SingleDistance))
                    double.TryParse(apply.SingleDistance.Replace(" 公里", ""), out km);

                isLongTrip = km > 30;   // ★ 超過 30 公里就是長差
            }

            // === 建立班表順序鏈 ===
            var chain = BuildShiftChain(useStart, isLongTrip);
            var early = new[] { "AM", "G1" }; // 早午
            var aft = new[] { "PM", "G2" }; // 午晚

            if (isLongTrip)
            {
                chain.Add("G3");
                if (inEarlyWindow) chain.AddRange(early);
                else if (inAfternoonWindow) chain.AddRange(aft);
                else chain.AddRange(early.Concat(aft));
            }
            else
            {
                if (inEarlyWindow) chain.AddRange(early);
                else if (inAfternoonWindow) chain.AddRange(aft);
                else chain.AddRange(early.Concat(aft));
            }

            chain.AddRange(new[] { "AM", "PM", "G1", "G2", "G3" });
            chain = chain.Distinct().ToList();

            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // === 1) 撈當日班表 ===
                var daySchedules = (await _db.Schedules
                    .Where(s => s.WorkDate == day && chain.Contains(s.Shift))
                    .AsNoTracking()
                    .ToListAsync())
                    .OrderBy(s => chain.IndexOf(s.Shift))
                    .ToList();

                // === 2) 撈代理人紀錄 ===
                var rawDelegs = await _db.DriverDelegations
                    .AsNoTracking()
                    .Where(d => d.StartDate.Date <= day && day <= d.EndDate.Date && d.PrincipalDriverId != null)
                    .ToListAsync();

                var delegMap = rawDelegs.ToDictionary(d => d.PrincipalDriverId!.Value, d => d.AgentId);

                // === 3) 建立最終可用班表（含代理人替換） ===
                var finalSchedules = new List<Cars.Models.Schedule>();
                foreach (var s in daySchedules)
                {
                    if (!s.IsPresent)
                    {
                        if (delegMap.TryGetValue(s.DriverId, out var agentId))
                        {
                            finalSchedules.Add(new Cars.Models.Schedule
                            {
                                WorkDate = s.WorkDate,
                                Shift = s.Shift,
                                DriverId = agentId,   // 代理人頂上
                                IsPresent = true       // 視為出勤
                            });
                            reasons.Add($"司機 {s.DriverId} 請假 → 改派代理人 {agentId}");
                        }
                        else
                        {
                            reasons.Add($"司機 {s.DriverId} 請假且無代理人");
                        }
                    }
                    else
                    {
                        finalSchedules.Add(s);
                    }
                }

                // === 4) 選擇一位司機 ===
                Cars.Models.Schedule? schedule = null;
                foreach (var s in finalSchedules)
                {
                    // 衝突檢查
                    var overlap = await _db.Dispatches.AnyAsync(d =>
                        d.DriverId == s.DriverId &&
                        localStart < d.EndTime &&
                        d.StartTime < localEnd);

                    if (overlap)
                    {
                        reasons.Add($"司機 {s.DriverId} 衝突：已有任務 {localStart:HH:mm}–{localEnd:HH:mm}");
                        continue;
                    }

                    // 長差休息限制
                    var longRest = await _db.Dispatches.AnyAsync(d =>
                        d.DriverId == s.DriverId &&
                        d.IsLongTrip &&
                        d.EndTime > localStart.AddHours(-1) &&
                        d.EndTime <= localStart);

                    if (longRest)
                    {
                        reasons.Add($"司機 {s.DriverId} 排除：長差未滿 1 小時");
                        continue;
                    }

                    schedule = s;
                    break;
                }

                if (schedule == null)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = $"當日({day:yyyy-MM-dd})無可派司機（順序：{string.Join(">", chain)}）\n" +
                                  string.Join("\n", reasons)
                    };
                }

                // === 5) 選車 ===
                int? chosenVehicleId = null;
                string? chosenPlate = null;

                if (!options.DriverOnly)
                {
                    if (options.PreferredVehicleId is int pv)
                    {
                        var v = await _db.Vehicles.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.VehicleId == pv && x.Status == "可用");
                        if (v == null)
                            return new DispatchResult { Success = false, Message = "指定車輛不可用或不存在" };

                        var used = await _db.Dispatches.AnyAsync(d =>
                            d.VehicleId == pv &&
                            localStart < d.EndTime &&
                            d.StartTime < localEnd);

                        if (used)
                            return new DispatchResult { Success = false, Message = "指定車輛該時段已被使用" };

                        chosenVehicleId = pv;
                        chosenPlate = v.PlateNo;
                    }
                    else
                    {
                        var candidates = await _db.Vehicles
                            .Where(v => v.Status == "可用" &&
                                        (passengerCount <= 0 || (v.Capacity == null || v.Capacity >= passengerCount)))
                            .AsNoTracking()
                            .ToListAsync();

                        foreach (var v in candidates)
                        {
                            var used = await _db.Dispatches.AnyAsync(d =>
                                d.VehicleId == v.VehicleId &&
                                localStart < d.EndTime &&
                                d.StartTime < localEnd);

                            if (!used)
                            {
                                chosenVehicleId = v.VehicleId;
                                chosenPlate = v.PlateNo;
                                break;
                            }
                        }

                        if (chosenVehicleId == null)
                        {
                            return new DispatchResult { Success = false, Message = "沒有符合時段/容量的可用車輛。" };
                        }
                    }
                }

                // === 6) 新增派工 ===
                var dispatch = new Cars.Models.Dispatch
                {
                    ApplyId = carApplyId,
                    DriverId = schedule.DriverId,
                    VehicleId = chosenVehicleId,
                    StartTime = localStart,
                    EndTime = localEnd,
                    CreatedAt = DateTime.UtcNow,
                    DispatchStatus = options.DriverOnly ? "已派駕駛" : "已派車",
                    IsLongTrip = isLongTrip
                };

                _db.Dispatches.Add(dispatch);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                var driverName = await _db.Drivers
                    .Where(d => d.DriverId == schedule.DriverId)
                    .Select(d => d.DriverName)
                    .FirstAsync();

                return new DispatchResult
                {
                    Success = true,
                    Message = options.DriverOnly ? "已派駕駛（待派車）" : "派工成功",
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
        public async Task<int?> FindOnDutyDriverIdAsync(DateTime useStart, DateTime useEnd, bool isLongTrip)
        {
            var day = useStart.Date;

            // 早午 07:30–11:30、午晚 13:30–17:00
            static bool InWindow(DateTime t, double fromHour, double toHour)
                => t.TimeOfDay >= TimeSpan.FromHours(fromHour) && t.TimeOfDay < TimeSpan.FromHours(toHour);

            bool isEarly = InWindow(useStart, 7.5, 11.5);   // 07:30–11:30
            bool isAfternoon = InWindow(useStart, 13.5, 17); // 13:30–17:00

            // 產生班別優先序（長差：G3 第一順位）
            List<string> chain = new();
            var early = new[] { "AM", "G1" };
            var aft = new[] { "PM", "G2" };

            if (isLongTrip)
            {
                chain.Add("G3");                         // ★ 長差第一順位
                if (isEarly) chain.AddRange(early);
                else if (isAfternoon) chain.AddRange(aft);
                else chain.AddRange(early.Concat(aft));
            }
            else
            {
                if (isEarly) chain.AddRange(early);
                else if (isAfternoon) chain.AddRange(aft);
                else chain.AddRange(early.Concat(aft));
                chain.Add("G3"); // 非長差放在最後備援（如果你不想讓非長差用到 G3，就把這行拿掉）
            }

            // 抓當日符合優先順序的班表，依 chain 排序
            var daySchedules = (await _db.Schedules
                    .Where(s => s.WorkDate == day && chain.Contains(s.Shift))
                    .AsNoTracking()
                    .ToListAsync())
                .OrderBy(s => chain.IndexOf(s.Shift))
                .ToList();

            foreach (var s in daySchedules)
            {
                // 司機同時段不可已有派遣（避免衝突）
                var busy = await _db.Dispatches.AnyAsync(d =>
                    d.DriverId == s.DriverId &&
                    useStart < d.EndTime &&
                    d.StartTime < useEnd);

                if (!busy) return s.DriverId;
            }

            return null; // 找不到可用駕駛
        }        // 管理員審核用：指定派工 ID，直接派車
        public async Task<DispatchResult> ApproveAndAssignVehicleAsync(
     int dispatchId, int passengerCount, int? preferredVehicleId = null)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.DispatchId == dispatchId);
            if (d == null) return new DispatchResult { Success = false, Message = "派工不存在" };

            // ★ 司機已經派好了：不許動 DriverId
            if (d.DriverId == null)
                return new DispatchResult { Success = false, Message = "此派工尚未指派駕駛" };

            // 冪等：若已派車，直接回成功（回傳現有車牌）
            if (d.VehicleId != null)
            {
                var plateExisting = await _db.Vehicles
                    .Where(v => v.VehicleId == d.VehicleId)
                    .Select(v => v.PlateNo)
                    .FirstOrDefaultAsync();

                return new DispatchResult
                {
                    Success = true,
                    Message = $"已派車 - {plateExisting}",
                    DriverId = d.DriverId,
                    VehicleId = d.VehicleId,
                    PlateNo = plateExisting
                };
            }

            var start = d.StartTime;
            var end = d.EndTime;

            // 指定車：只檢查可用性/容量/時段衝突（排除已取消）
            if (preferredVehicleId.HasValue)
            {
                var pv = preferredVehicleId.Value;

                var v = await _db.Vehicles
                    .FirstOrDefaultAsync(x => x.VehicleId == pv && (x.Status ?? "") == "可用");
                if (v == null) return new DispatchResult { Success = false, Message = "指定車輛不可用或不存在" };

                if (v.Capacity.HasValue && passengerCount > 0 && v.Capacity.Value < passengerCount)
                    return new DispatchResult { Success = false, Message = "指定車輛座位數不足" };

                var used = await _db.Dispatches.AnyAsync(x =>
                    x.VehicleId == pv &&
                    x.DispatchStatus != "已取消" &&                  // ★ 排除取消
                    start < x.EndTime && x.StartTime < end);

                if (used) return new DispatchResult { Success = false, Message = "指定車輛該時段已被使用" };

                d.VehicleId = pv;
                d.DispatchStatus = "已派車";
                await _db.SaveChangesAsync();

                return new DispatchResult
                {
                    Success = true,
                    Message = "已派車",
                    DriverId = d.DriverId,
                    VehicleId = d.VehicleId,
                    PlateNo = v.PlateNo
                };
            }

            // 自動挑第一台可用車（可用狀態、容量 OK、時段不衝突；排除取消單）
            var candidates = await _db.Vehicles
                .Where(v => (v.Status ?? "") == "可用"
                            && (passengerCount <= 0 || v.Capacity == null || v.Capacity >= passengerCount))
                .AsNoTracking()
                .ToListAsync();

            foreach (var v in candidates)
            {
                Console.WriteLine($"檢查車 {v.VehicleId} {v.PlateNo}, Status={v.Status}, Capacity={v.Capacity}");

                var used = await _db.Dispatches.AnyAsync(x =>
                x.VehicleId == v.VehicleId && 
                start < x.EndTime &&
                x.StartTime < end);

                if (used) continue;
                Console.WriteLine($" -> used={used}");

                // 配車成功
                d.VehicleId = v.VehicleId;
                d.DispatchStatus = "已派車";
                await _db.SaveChangesAsync();

                return new DispatchResult
                {
                    Success = true,
                    Message = "已派車",
                    DriverId = d.DriverId,
                    VehicleId = d.VehicleId,
                    PlateNo = v.PlateNo
                };
            }

            return new DispatchResult { Success = false, Message = "沒有符合時段/容量的可用車輛" };
        }


    }


}
