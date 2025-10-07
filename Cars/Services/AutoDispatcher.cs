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

        #region 班表優先順序鏈建構
        private static List<string> BuildShiftChain(DateTime start, bool isLongTrip)
        {
            var t = start.TimeOfDay;
            bool early = t < TimeSpan.FromHours(11.5);
            bool aft = t >= TimeSpan.FromHours(13.5) && t < TimeSpan.FromHours(17);

            var chain = new List<string>();

            if (isLongTrip)
            {
                // 長差優先順序：G3 → G2 → 其他
                chain.Add("G3");      // 第一順位
                chain.Add("G2");      // 第二順位
                if (early) chain.Add("AM");
                else if (aft) chain.Add("PM");
                else chain.AddRange(new[] { "AM", "PM", "G1" });
            }
            else
            {
                // 非長差：AM/PM/G1 → 最後才輪到 G2,G3 當備援
                if (early) chain.AddRange(new[] { "AM", "G1" });
                else if (aft) chain.AddRange(new[] { "PM", "G2" });
                else chain.AddRange(new[] { "AM", "PM", "G1", "G2" });
                chain.Add("G3"); // 最後備援
            }

            // 保險起見，仍可加上完整備援鏈
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
        #endregion

        #region 自動派工
        //自動派遣司機
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

            var T0730 = TimeSpan.FromHours(7.5);
            var T1130 = TimeSpan.FromHours(11.5);
            var T1330 = TimeSpan.FromHours(13.5);
            var T1700 = TimeSpan.FromHours(17);

            bool OverlapWin(DateTime s, DateTime e, TimeSpan ws, TimeSpan we)
                => s.TimeOfDay < we && ws < e.TimeOfDay;

            bool inEarlyWindow = OverlapWin(localStart, localEnd, T0730, T1130);
            bool inAfternoonWindow = OverlapWin(localStart, localEnd, T1330, T1700);

            // === 判斷長差 ===
            var apply = await _db.CarApplications.AsNoTracking().FirstOrDefaultAsync(x => x.ApplyId == carApplyId);
            bool isLongTrip = false;
            if (apply != null)
            {
                decimal km = 0;

                if (apply.RoundTripDistance.HasValue)
                    km = apply.RoundTripDistance.Value;
                else if (apply.SingleDistance.HasValue)
                    km = apply.SingleDistance.Value;

                isLongTrip = km > 30;
            }


            // === 建立班表順序鏈 ===
            var chain = BuildShiftChain(useStart, isLongTrip);
            var early = new[] { "AM", "G1" };
            var aft = new[] { "PM", "G2" };

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
               
                // === 2) 撈代理人紀錄（改用 AgentDriverId → Drivers.DriverId）★
                var rawDelegs = await _db.DriverDelegations
                    .AsNoTracking()
                    .Where(d => d.StartDate.Date <= day && day <= d.EndDate.Date )
                    .ToListAsync();

                // 若同一天同一被代理人有多筆，取最新建立的那筆
                var delegMap = rawDelegs
                .GroupBy(d => d.PrincipalDriverId) // 直接用 int
                .ToDictionary(
                 g => g.Key,
                 g => g.OrderByDescending(x => x.CreatedAt).First().AgentDriverId
                 );



                // === 3) 建立最終可用班表（缺勤 → 代理頂上），代理人無固定班表：動態建立「臨時代理班表」 ===
                

                var driverNames = await _db.Drivers
                    .ToDictionaryAsync(d => d.DriverId, d => d.DriverName);

                // 小工具：容忍 null 的 DriverId
                string GetName(int? id) =>
                    (id.HasValue && driverNames.TryGetValue(id.Value, out var nm))
                        ? nm
                        : (id.HasValue ? $"ID={id.Value}" : "—");

                // 分類：代理/一般；注意 DriverId 可能為 null
                var replaced = new List<Cars.Models.Schedule>();
                var normal = new List<Cars.Models.Schedule>();

                foreach (var s in daySchedules)
                {
                    string drvName = driverNames.ContainsKey(s.DriverId.Value) ? driverNames[s.DriverId.Value] : $"ID={s.DriverId}";

                   
                }

                // 代理頂替優先，再接一般到班；仍依 chain 排序
                var finalSchedules = replaced.Concat(normal)
                    .OrderBy(s => chain.IndexOf(s.Shift))
                    .ToList();

                // 去重：若同一代理人在清單中同時出現（自己原班 + 臨時頂替），保留較前者（通常是臨時頂替）
                var seen = new HashSet<int>();
                finalSchedules = finalSchedules.Where(s => seen.Add(s.DriverId.Value)).ToList();


                // === 4) 選擇一位司機（依 chain 順序，排除衝突/長差休息） ===
                Cars.Models.Schedule? schedule = null;
                foreach (var s in finalSchedules)
                {
                    // 衝突檢查
                    var conflict = await _db.Dispatches
                        .Include(d => d.Driver)   
                        .Where(d => d.DriverId == s.DriverId &&
                                    localStart < d.EndTime &&
                                    d.StartTime < localEnd)
                        .OrderBy(d => d.StartTime)
                        .FirstOrDefaultAsync();

                    if (conflict != null)
                    {
                        var conflictDriverName = conflict.Driver?.DriverName ?? $"ID={s.DriverId}";
                        reasons.Add($"司機 {conflictDriverName} 衝突：已有任務 {conflict.StartTime:HH:mm}–{conflict.EndTime:HH:mm}");
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
                        // 注意：不要叫 driverName，避免和外層同名
                        var displayName = await _db.Drivers
                            .Where(dr => dr.DriverId == s.DriverId)
                            .Select(dr => dr.DriverName)
                            .FirstOrDefaultAsync();

                        reasons.Add($"司機 {displayName ?? s.DriverId.ToString()} 排除：長差未滿 1 小時");
                        continue;
                    }





                    // 後段任務檢查
                    var laterConflict = await _db.Dispatches.AnyAsync(d =>
                        d.DriverId == s.DriverId &&
                        d.StartTime > localStart &&   
                        d.StartTime.Value.Date == localStart.Date);

                    // ★ 取消後段任務檢查，避免無法派工
                    //if (laterConflict)
                    //{
                    //    string drvName = driverNames.ContainsKey(s.DriverId) ? driverNames[s.DriverId] : $"ID={s.DriverId}";
                    //    reasons.Add($"司機 {drvName} 排除：當日稍晚已有任務");
                    //    continue;
                    //}
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

                // === 5) 選車（原邏輯） ===
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
                            return new DispatchResult { Success = false, Message = "沒有符合時段/載客量的可用車輛。" };
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
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "資料已被更新，請重新整理後再試：" + ex.Message
                    };
                }
                catch (DbUpdateException ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "資料儲存失敗，請確認輸入是否正確：" + (ex.InnerException?.Message ?? ex.Message)
                    };
                }
                catch (Exception ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "伺服器內部錯誤：" + ex.Message
                    };
                }
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
                chain.Add("G3"); // 非長差放在最後備援
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
        }
        #endregion

        #region 審核後派車
        // 管理員審核用：指定派工 ID，直接派車
        public async Task<DispatchResult> ApproveAndAssignVehicleAsync(
      int dispatchId,
      int passengerCount,
      int? preferredVehicleId = null)
        {
            var d = await _db.Dispatches.FirstOrDefaultAsync(x => x.DispatchId == dispatchId);
            if (d == null) return new DispatchResult { Success = false, Message = "派工不存在" };

            // 駕駛必須已經指派
            //if (d.DriverId == null)
            //    return new DispatchResult { Success = false, Message = "此派工尚未指派駕駛" };
            // 從 CarApplications 取得用車時間
            var app = await _db.CarApplications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ApplyId == d.ApplyId);

            if (app == null)
                return new DispatchResult { Success = false, Message = "找不到對應的用車申請單。" };

            if (app.UseStart == default || app.UseEnd == default)
                return new DispatchResult { Success = false, Message = "申請單未設定起訖時間，無法派車。" };

            var start = app.UseStart;
            var end = app.UseEnd;
            // 已經派過車就回覆現況
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
                    PlateNo = plateExisting,
                };
            }


            // 檢查可用/載客量/時段衝突
            if (preferredVehicleId.HasValue)
            {
                var v = await _db.Vehicles
                    .FirstOrDefaultAsync(x => x.VehicleId == preferredVehicleId.Value && (x.Status ?? "") == "可用");
                if (v == null) return new DispatchResult { Success = false, Message = "指定車輛不可用或不存在" };

                if (v.Capacity.HasValue && passengerCount > 0 && v.Capacity.Value < passengerCount)
                    return new DispatchResult { Success = false, Message = "指定車輛座位數不足" };

                var used = await _db.Dispatches
                    .Where(d => d.VehicleId == v.VehicleId)
                    .Join(_db.CarApplications,
                        dis => dis.ApplyId,
                        app => app.ApplyId,
                        (dis, app) => new { dis, app })
                    .AnyAsync(x => start < x.app.UseEnd && x.app.UseStart < end);



                if (used) return new DispatchResult { Success = false, Message = "指定車輛該時段已被使用" };

                d.VehicleId = v.VehicleId;
                d.DispatchStatus = "已派車";
                app.VehicleId = v.VehicleId;
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "資料已被更新，請重新整理後再試：" + ex.Message
                    };
                }
                catch (DbUpdateException ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "資料儲存失敗，請確認輸入是否正確：" + (ex.InnerException?.Message ?? ex.Message)
                    };
                }
                catch (Exception ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "伺服器內部錯誤：" + ex.Message
                    };
                }

                return new DispatchResult
                {
                    Success = true,
                    Message = "已派車",
                    DriverId = d.DriverId,
                    VehicleId = d.VehicleId,
                    PlateNo = v.PlateNo
                };
            }

            // ── 自動挑車（平均使用）
            // 1) 候選車（可用 + 座位數夠）
            var candidates = await _db.Vehicles
                .Where(v => (v.Status ?? "") == "可用"
                         && (passengerCount <= 0 || v.Capacity == null || v.Capacity >= passengerCount))
                .AsNoTracking()
                .ToListAsync();

            // 2) 累積里程（排除取消單）
            var vehicleUsage = await _db.Dispatches
                .Where(x => x.VehicleId != null)   
                .Join(_db.CarApplications,
                    dis => dis.ApplyId,
                    app => app.ApplyId,
                    (dis, app) => new {
                        dis.VehicleId,
                        Distance = dis.IsLongTrip
                            ? (app.RoundTripDistance ?? app.SingleDistance ?? 0)
                            : (app.SingleDistance ?? app.RoundTripDistance ?? 0),
                        LastUsedAt = dis.EndTime
                    })
                .GroupBy(x => x.VehicleId.Value)
                .Select(g => new {
                    VehicleId = g.Key,
                    TotalKm = g.Sum(x => x.Distance),
                    LastUsedAt = g.Max(x => x.LastUsedAt)   // 取最後一次使用時間
                })
                .ToListAsync();

            var kmDict = vehicleUsage.ToDictionary(x => x.VehicleId, x => x.TotalKm);
            var lastDict = vehicleUsage.ToDictionary(x => x.VehicleId, x => x.LastUsedAt ?? DateTime.MinValue);

            // 3) 平均排序：TotalKm 少 → LastUsedAt 越久沒用 → VehicleId
            var ordered = candidates
                .OrderBy(v => kmDict.ContainsKey(v.VehicleId) ? kmDict[v.VehicleId] : 0)
                .ThenBy(v => lastDict.ContainsKey(v.VehicleId) ? lastDict[v.VehicleId] : DateTime.MinValue)
                .ThenBy(v => v.VehicleId)
                .ToList();

            // 4) 逐台檢查時段衝突（排除取消），選到即用
            foreach (var v in ordered) 
            {
                var used = await (
                    from dis in _db.Dispatches
                    join app2 in _db.CarApplications on dis.ApplyId equals app2.ApplyId
                    where dis.VehicleId == v.VehicleId
                          && app2.ApplyId != app.ApplyId  
                          && start < app2.UseEnd
                          && app2.UseStart < end
                    select dis
                ).AnyAsync();


                if (used) continue;

                d.VehicleId = v.VehicleId;
                d.DispatchStatus = "已派車";
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "資料已被更新，請重新整理後再試：" + ex.Message
                    };
                }
                catch (DbUpdateException ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "資料儲存失敗，請確認輸入是否正確：" + (ex.InnerException?.Message ?? ex.Message)
                    };
                }
                catch (Exception ex)
                {
                    return new DispatchResult
                    {
                        Success = false,
                        Message = "伺服器內部錯誤：" + ex.Message
                    };
                }

                return new DispatchResult
                {
                    Success = true,
                    Message = "已派車",
                    DriverId = d.DriverId,
                    VehicleId = d.VehicleId,
                    PlateNo = v.PlateNo
                };
            }

            return new DispatchResult { Success = false, Message = "沒有符合時段/載客量的可用車輛" };
        }

        #endregion

        #region 派工結果
        public sealed class DispatchResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public int? DriverId { get; set; }
            public int? VehicleId { get; set; }
            public string? DriverName { get; set; }
            public string? PlateNo { get; set; }
        }
        #endregion


    }


}
