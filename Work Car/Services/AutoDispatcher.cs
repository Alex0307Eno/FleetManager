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
    var localEnd   = useEnd;
    var day        = localStart.Date;

    // === 時窗（早午／午晚） ===
    var T0730 = TimeSpan.FromHours(7.5);   // 07:30
    var T1130 = TimeSpan.FromHours(11.5);  // 11:30
    var T1330 = TimeSpan.FromHours(13.5);  // 13:30
    var T1700 = TimeSpan.FromHours(17);    // 17:00

    bool OverlapWin(DateTime s, DateTime e, TimeSpan ws, TimeSpan we)
        => s.TimeOfDay < we && ws < e.TimeOfDay;

    bool inEarlyWindow     = OverlapWin(localStart, localEnd, T0730, T1130);  // 07:30–11:30
    bool inAfternoonWindow = OverlapWin(localStart, localEnd, T1330, T1700);  // 13:30–17:00

            var apply = await _db.CarApplications
             .AsNoTracking()
             .FirstOrDefaultAsync(x => x.ApplyId == carApplyId);

            bool isLongTrip = false;

            if (apply != null)
            {
                // 優先用 RoundTripDistance，沒有就用 SingleDistance
                double km = 0;
                if (!string.IsNullOrEmpty(apply.RoundTripDistance))
                    double.TryParse(apply.RoundTripDistance.Replace(" 公里", ""), out km);
                else if (!string.IsNullOrEmpty(apply.SingleDistance))
                    double.TryParse(apply.SingleDistance.Replace(" 公里", ""), out km);

                isLongTrip = km > 30;   // ★ 超過 30 公里就是長差
            }

            // === 產生派車優先順序：G3 視為「長差班」第一順位 ===
            var chain = BuildShiftChain(useStart, isLongTrip);
    var early = new[] { "AM", "G1" }; // 早午
    var aft   = new[] { "PM", "G2" }; // 午晚

    if (isLongTrip)
    {
        chain.Add("G3"); // 長差班第一順位
        if (inEarlyWindow)          chain.AddRange(early);
        else if (inAfternoonWindow) chain.AddRange(aft);
        else                        chain.AddRange(early.Concat(aft));
    }
    else
    {
        if (inEarlyWindow)          chain.AddRange(early);
        else if (inAfternoonWindow) chain.AddRange(aft);
        else                        chain.AddRange(early.Concat(aft));
    }

    chain.AddRange(new[] { "AM", "PM", "G1", "G2","G3" });
    chain = chain.Distinct().ToList();

    using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    try
    {
        // 1) 依 chain 找當日班表
        var daySchedules = (await _db.Schedules
            .Where(s => s.WorkDate == day && chain.Contains(s.Shift))
            .AsNoTracking()
            .ToListAsync())
            .OrderBy(s => chain.IndexOf(s.Shift))
            .ToList();

        Cars.Models.Schedule? schedule = null;

        foreach (var s in daySchedules)
        {
            // 2) 衝突檢查：時間重疊 → 衝突
            var overlap = await _db.Dispatches.AnyAsync(d =>
                d.DriverId == s.DriverId &&
                localStart < d.EndTime &&
                d.StartTime < localEnd);

            if (overlap) continue;

            // 3) ★ 長差休息規則：只對「前一趟為長差」的人套用 1h 冷卻
            // 建議在 Dispatch 模型加一個 IsLongTrip(bool) 欄位，下面查詢就能成立
            var longRest = await _db.Dispatches.AnyAsync(d =>
                d.DriverId == s.DriverId &&
                d.IsLongTrip &&                                  // ← 前一趟是長差
                d.EndTime > localStart.AddHours(-1) && d.EndTime <= localStart); // ← 距離現在 < 1h

            if (longRest) continue;

            schedule = s;
            break;
        }

        if (schedule == null)
        {
            return new DispatchResult {
                Success = false,
                Message = $"當日({day:yyyy-MM-dd})無可派司機（順序：{string.Join(">", chain)}），或受長差休息限制。"
            };
        }

        // === 只派駕駛（預設） ===
        int? chosenVehicleId = null;
        string? chosenPlate  = null;

        if (!options.DriverOnly)
        {
            // 管理員審核後才會走這段（自動派車）
            if (options.PreferredVehicleId is int pv)
            {
                // 指定車輛
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
                chosenPlate     = v.PlateNo;
            }
            else
            {
                // 自動挑第一台可用車
                var candidates = await _db.Vehicles
                    .Where(v => v.Status == "可用" &&
                                (passengerCount <= 0 || (v.Capacity == null || v.Capacity >= passengerCount)))
                    .AsNoTracking().ToListAsync();

                foreach (var v in candidates)
                {
                    var used = await _db.Dispatches.AnyAsync(d =>
                        d.VehicleId == v.VehicleId &&
                        localStart < d.EndTime &&
                        d.StartTime < localEnd);
                    if (!used) { chosenVehicleId = v.VehicleId; chosenPlate = v.PlateNo; break; }
                }

                if (chosenVehicleId == null)
                {
                    return new DispatchResult { Success = false, Message = "沒有符合時段/容量的可用車輛。" };
                }
            }
        }
                // ★ 先找這張申請單是否已經有派工（未取消/未完成的最新一筆）
                var existing = await _db.Dispatches
                    .Where(d => d.ApplyId == carApplyId && d.DispatchStatus != "已取消")
                    .OrderByDescending(d => d.DispatchId)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    // 補司機
                    if (existing.DriverId == null)
                        existing.DriverId = schedule.DriverId;

                    // 如本次為「要派車」且原派工未指派車，補車輛
                    if (!options.DriverOnly && existing.VehicleId == null)
                    {
                        // 這兩個變數就是你後面本來就算出的結果
                        existing.VehicleId = chosenVehicleId;
                        existing.DispatchStatus = "已派車";
                    }
                    else if (existing.DispatchStatus != "已派車")
                    {
                        existing.DispatchStatus = "已派駕駛";
                    }

                    // 同步長差標記
                    existing.IsLongTrip = isLongTrip;

                    _db.Dispatches.Update(existing);
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();

                    // 回傳既有派工的結果（讓前端能顯示車牌）
                    var driverName2 = await _db.Drivers
                        .Where(d => d.DriverId == existing.DriverId)
                        .Select(d => d.DriverName)
                        .FirstOrDefaultAsync();

                    return new DispatchResult
                    {
                        Success = true,
                        Message = (!options.DriverOnly && existing.VehicleId != null) ? "已派車（沿用既有派工）" : "已派駕駛（沿用既有派工）",
                        DriverId = existing.DriverId,
                        VehicleId = existing.VehicleId,
                        DriverName = driverName2,
                        PlateNo = chosenPlate
                    };
                }


                // 4) 寫入派工：Driver 一定會有；Vehicle 視 DriverOnly 而定
                var dispatch = new Cars.Models.Dispatch
        {
            ApplyId        = carApplyId,
            DriverId       = schedule.DriverId,
            VehicleId      = chosenVehicleId,           // 只派駕駛時保持 null
            StartTime      = localStart,
            EndTime        = localEnd,
            CreatedAt      = DateTime.UtcNow,
            DispatchStatus = options.DriverOnly ? "已派駕駛" : "已派車",
            IsLongTrip     = isLongTrip                 // ★ 建議在 Dispatch 模型加這欄位
        };

        _db.Dispatches.Add(dispatch);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var driverName = await _db.Drivers
            .Where(d => d.DriverId == schedule.DriverId)
            .Select(d => d.DriverName)
            .FirstAsync();

        return new DispatchResult {
            Success    = true,
            Message    = options.DriverOnly ? "已派駕駛（待派車）" : "派工成功",
            DriverId   = schedule.DriverId,
            VehicleId  = chosenVehicleId,
            DriverName = driverName,
            PlateNo    = chosenPlate
        };
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        return new DispatchResult {
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
            int dispatchId,
            int passengerCount,
            int? preferredVehicleId = null)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.DispatchId == dispatchId);
            if (d == null) return new DispatchResult { Success = false, Message = "派工不存在" };

            if (d.VehicleId != null)
                return new DispatchResult { Success = false, Message = "此派工已派車" };

            var start = d.StartTime;
            var end = d.EndTime;

            int? chosenVehicleId = null;
            string? plate = null;

            if (preferredVehicleId is int pv)
            {
                var v = await _db.Vehicles.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.VehicleId == pv && x.Status == "可用");
                if (v == null) return new DispatchResult { Success = false, Message = "指定車輛不可用或不存在" };

                var used = await _db.Dispatches.AnyAsync(x =>
                    x.VehicleId == pv && start < x.EndTime && x.StartTime < end);
                if (used) return new DispatchResult { Success = false, Message = "指定車輛該時段已被使用" };

                chosenVehicleId = pv; plate = v.PlateNo;
            }
            else
            {
                var candidates = await _db.Vehicles
                    .Where(v => v.Status == "可用" &&
                                (passengerCount <= 0 || (v.Capacity == null || v.Capacity >= passengerCount)))
                    .AsNoTracking().ToListAsync();

                foreach (var v in candidates)
                {
                    var used = await _db.Dispatches.AnyAsync(x =>
                        x.VehicleId == v.VehicleId && start < x.EndTime && x.StartTime < end);
                    if (!used) { chosenVehicleId = v.VehicleId; plate = v.PlateNo; break; }
                }

                if (chosenVehicleId == null)
                    return new DispatchResult { Success = false, Message = "沒有符合時段/容量的可用車輛。" };
            }

            d.VehicleId = chosenVehicleId;
            d.DispatchStatus = "已派車";
            await _db.SaveChangesAsync();

            return new DispatchResult
            {
                Success = true,
                Message = "審核完成，已派車",
                DriverId = d.DriverId,
                VehicleId = d.VehicleId,
                PlateNo = plate
            };
        }

    }


}
