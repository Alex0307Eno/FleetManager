using Cars.Data;
using Cars.Shared.Dtos.DashBoard;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.ApiControllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public DashboardController(ApplicationDbContext db) 
        {
            _db = db; 
        }
        #region 卡片數字
        //  卡片數字
        [HttpGet("cards")]
        public async Task<IActionResult> Cards()
        {
            var today = DateTime.Today;

            var scheduleTodayCount = await _db.Schedules.CountAsync(s => s.WorkDate == today);

            var uncompleteCount = await _db.Dispatches.CountAsync(d =>
                d.DispatchStatus != "已完成" ||
                d.DriverId == null || d.VehicleId == null || d.DispatchStatus == "待派車");

            var pendingReviewCount = await _db.CarApplications.CountAsync(a =>
                a.Status == "待審核" || a.Status == "審核中");

            var result = new
            {
                scheduleTodayCount,
                uncompleteCount,
                pendingReviewCount
            };

            return Ok(ApiResponse<object>.Ok(result, "儀表板卡片統計成功"));
        }
        #endregion

        #region 今日排班
        //  今日排班
        [HttpGet("schedule/today")]
        public async Task<IActionResult> TodaySchedule()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);


            var list = await (
                from s in _db.Schedules
                where s.WorkDate == today

                // 先用 LineCode 的歷史對應補 DriverId（生效期間含今天）
                join dla0 in _db.DriverLineAssignments
                    .Where(a => a.StartDate <= today && (a.EndDate == null || a.EndDate >= today))
                    on s.LineCode equals dla0.LineCode into gj
                from dla in gj.DefaultIfEmpty()

                    // 解析當天應該顯示的 DriverId
                let resolvedDriverId = (int?)(s.DriverId ?? (dla != null ? dla.DriverId : (int?)null))

                // 駕駛
                join d0 in _db.Drivers on resolvedDriverId equals (int?)d0.DriverId into gd
                from d in gd.DefaultIfEmpty()

                    // 代理人邏輯
                join dg0 in _db.DriverDelegations
                    .Where(x => x.StartDate <= today && today <= x.EndDate)
                    on resolvedDriverId equals (int?)dg0.PrincipalDriverId into dgs
                from dg in dgs.DefaultIfEmpty()

                join agent0 in _db.Drivers
                    on (int?)(dg != null ? dg.AgentDriverId : (int?)null)
                    equals (int?)agent0.DriverId into ags
                from agent in ags.DefaultIfEmpty()

                let showDriverId =
                    (dg != null && agent != null)
                        ? (int?)agent.DriverId
                        : resolvedDriverId

                let showDriverName =
                    (dg != null && agent != null)
                        ? (agent.DriverName + " (代)")
                        : (d != null ? d.DriverName : null)

                // 展開當天派工（有用車時間者）
                from dis in _db.Dispatches
                    .Where(x => showDriverId != null
                                && x.DriverId == showDriverId)
                    .DefaultIfEmpty()

                join a0 in _db.CarApplications
                    on dis.ApplyId equals a0.ApplyId into aa
                from a in aa.DefaultIfEmpty()

                join v0 in _db.Vehicles
                    on dis.VehicleId equals v0.VehicleId into vv
                from v in vv.DefaultIfEmpty()

                join ap0 in _db.Applicants
                    on a.ApplicantId equals ap0.ApplicantId into appGroup
                from ap in appGroup.DefaultIfEmpty()
                where a.UseStart != null && a.UseStart >= today && a.UseStart < tomorrow

                orderby a.UseStart, dis.DispatchId
                // 排序以「用車開始時間」為主，若無則用派工時間
                let sortTime =
                    (a != null && a.UseStart != null)
                        ? a.UseStart
                        : (dis.StartTime ?? today.AddHours(23))

                orderby sortTime, (dis != null ? dis.DispatchId : int.MaxValue)

                select new TodayScheduleDto
                {
                    ScheduleId = s.ScheduleId,
                    Shift = s.Shift,
                    DriverId = showDriverId.Value,
                    DriverName = showDriverName,
                    HasDispatch = (dis != null),
                    UseStart = (a != null ? a.UseStart : null),
                    UseEnd = (a != null ? a.UseEnd : null),
                    Route = (a != null ? ((a.Origin ?? "") + "-" + (a.Destination ?? "")) : ""),
                    ApplicantName = (ap != null ? ap.Name : null),
                    ApplicantDept = (ap != null ? ap.Dept : null),
                    PassengerCount = (a != null ? a.PassengerCount : 0),
                    PlateNo = (v != null ? v.PlateNo : null),
                    TripDistance = (a != null
                        ? (a.TripType == "單程"
                            ? (a.SingleDistance ?? 0)
                            : (a.RoundTripDistance ?? 0))
                        : 0)
                }
            ).ToListAsync();

            return Ok(ApiResponse<List<TodayScheduleDto>>.Ok(list, "今日班表查詢成功"));
        }


        #endregion

        #region 駕駛目前狀態
        //駕駛目前狀態
        [HttpGet("drivers/today-status")]
        public async Task<ActionResult<ApiResponse<List<DriverStatusDto>>>> DriversTodayStatus()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            var oneHourAgo = now.AddHours(-1);

            // === Step 1. 駕駛基本資料 + 今日班別 ===
            var baseDrivers = await (
                from d in _db.Drivers.Where(x => !x.IsAgent)
                    // 找出今天是否有代理
                join del in _db.DriverDelegations
                    .Where(x => x.StartDate <= today && today <= x.EndDate)
                    on d.DriverId equals del.PrincipalDriverId into delg
                from dg in delg.DefaultIfEmpty()

                    // 找出代理人
                join agent in _db.Drivers on dg.AgentDriverId equals agent.DriverId into ag
                from a in ag.DefaultIfEmpty()
                let showDriverId = (dg != null && a != null) ? a.DriverId : d.DriverId

                join s in _db.Schedules.Where(s => s.WorkDate == today)
                    on showDriverId equals s.DriverId into sg
                from s in sg.DefaultIfEmpty()
                select new
                {
                    d.DriverId,
                    d.DriverName,
                    Shift = s != null ? s.Shift : null,
                }
            ).AsNoTracking().ToListAsync();

            // === Step 2. 當前派工（正在執勤的） ===
            var dispatchesNow = await (
                from dis in _db.Dispatches
                join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId
                join v in _db.Vehicles on dis.VehicleId equals v.VehicleId into vv
                from v in vv.DefaultIfEmpty()
                where dis.DriverId != null
                      && dis.VehicleId != null
                      //  若 EndTime 沒填，則以申請單的時間區間為準
                      && (
                           (!dis.EndTime.HasValue && a.UseStart <= now && a.UseEnd >= now)
                           //  若 EndTime 已填，代表任務結束，不該再視為執勤
                           || (dis.EndTime.HasValue && dis.EndTime > now.AddMinutes(-5))
                         )
                select new DispatchMapView
                {
                    DriverId = dis.DriverId.Value,
                    UseStart = a.UseStart,
                    UseEnd = a.UseEnd,
                    PlateNo = v != null ? v.PlateNo : null,
                    Dept = ap.Dept,
                    Name = ap.Name,
                    PassengerCount = a.PassengerCount
                }
            ).AsNoTracking().ToListAsync();



            var dispatchByDriver = dispatchesNow
                .GroupBy(x => x.DriverId!)
                .ToDictionary(g => g.Key, g => g.First());

            // === Step 3. 查代理人 ===
            var delegs = await (
                from dg in _db.DriverDelegations
                where dg.StartDate.Date <= today && today <= dg.EndDate.Date
                join agent in _db.Drivers on dg.AgentDriverId equals agent.DriverId
                select new { dg.PrincipalDriverId, Agent = agent, dg.CreatedAt }
            ).AsNoTracking().ToListAsync();

            var delegMap = delegs
                .GroupBy(x => x.PrincipalDriverId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(z => z.CreatedAt).First().Agent);
            // 讓本尊與代理都能命中派工
            var expandedDispatch = new Dictionary<int, DispatchMapView>(dispatchByDriver);
            foreach (var kv in delegMap)
            {
                var principalId = kv.Key;
                var agentId = kv.Value.DriverId;
                if (dispatchByDriver.ContainsKey(principalId))
                    expandedDispatch[agentId] = dispatchByDriver[principalId];
                if (dispatchByDriver.ContainsKey(agentId))
                    expandedDispatch[principalId] = dispatchByDriver[agentId];
            }

            // === Step 4. 計算每位駕駛的最後完工時間 (EndTime 或 UseEnd) ===
            var tomorrow = today.AddDays(1);
            var restSpan = TimeSpan.FromHours(1);

            // === 找每位駕駛今日最後一次派工結束 ===
            var lastEndRaw = await (
                from dis in _db.Dispatches
                join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                where dis.DriverId != null
                      && (
                           (dis.EndTime != null && dis.EndTime >= today && dis.EndTime < tomorrow)
                           || (a.UseEnd >= today && a.UseEnd < tomorrow)
                         )
                select new
                {
                    dis.DriverId,
                    EndTime = dis.EndTime,
                    UseEnd = a.UseEnd
                }
            ).AsNoTracking().ToListAsync();

            // lastEndByDriver: 若有 EndTime 用它，否則用 UseEnd
            var lastEndByDriver = lastEndRaw
                .GroupBy(x => x.DriverId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Max(z =>
                        (DateTime?)z.EndTime
                        ?? (DateTime?)z.UseEnd
                        ?? DateTime.MinValue
                    )
                );


            // === Step 5. 最近用過的車（若今日沒派工） ===
            var recentVehicleByDriver = await (
                from dis in _db.Dispatches
                join v in _db.Vehicles on dis.VehicleId equals v.VehicleId
                where dis.DriverId != null && (dis.EndTime.HasValue || dis.StartTime.HasValue)
                orderby dis.EndTime descending
                select new { dis.DriverId, v.PlateNo, dis.EndTime }
            ).AsNoTracking().ToListAsync();

            var recentMap = recentVehicleByDriver
                .GroupBy(x => x.DriverId!.Value)
                .ToDictionary(g => g.Key, g => g.First().PlateNo);

            // === 組合結果 ===
            var result = new List<DriverStatusDto>();

            foreach (var d in baseDrivers)
            {
                var driverId = d.DriverId;
                var driverName = d.DriverName;
                var attendance = "正常";
                string shift = d.Shift;

                if (delegMap.TryGetValue(d.DriverId, out var proxyAgent))
                {
                    driverId = proxyAgent.DriverId;
                    driverName = $"{proxyAgent.DriverName}(代)";
                    attendance = $"請假({d.DriverName})";
                }

                var dispatch = dispatchByDriver.ContainsKey(driverId)
                    ? dispatchByDriver[driverId]
                    : null;

                string stateText = "待命中";
                DateTime? restUntil = null;
                int? restRemainMinutes = null;

                // 根據派工判斷狀態
                if (dispatch != null)
                {
                    // 若有開始時間、但未結束 → 執勤中
                    if (dispatch.UseStart <= now && (dispatch.UseEnd == null || dispatch.UseEnd > now))
                    {
                        stateText = "執勤中";
                    }
                    // 若已結束 → 進入休息中
                    else if (dispatch.UseEnd != null && dispatch.UseEnd <= now)
                    {
                        var end = dispatch.UseEnd;
                        restUntil = end.AddHours(1);
                        var remaining = (restUntil.Value - now).TotalMinutes;
                        if (remaining > 0 && remaining <= 60)
                        {
                            restRemainMinutes = (int)Math.Ceiling(remaining);
                            stateText = "休息中";
                        }
                        else
                        {
                            restRemainMinutes = null;
                            stateText = "待命中";
                        }

                    }
                }
                else if (lastEndByDriver.TryGetValue(driverId, out var lastEnd) && lastEnd > DateTime.MinValue)
                {
                    // 沒有正在執勤，但今天有完成過任務
                    restUntil = lastEnd.AddHours(1);
                    if (now < restUntil)
                    {
                        restRemainMinutes = (int)Math.Ceiling((restUntil.Value - now).TotalMinutes);
                        stateText = "休息中";
                    }
                }

                string plateNo = dispatch?.PlateNo;
                if (string.IsNullOrEmpty(plateNo) && recentMap.ContainsKey(driverId))
                    plateNo = recentMap[driverId];

                result.Add(new DriverStatusDto
                {
                    DriverId = driverId,
                    DriverName = driverName ?? "-",
                    Shift = shift,
                    PlateNo = plateNo,
                    ApplicantDept = dispatch?.Dept,
                    ApplicantName = dispatch?.Name,
                    PassengerCount = dispatch?.PassengerCount,
                    UseStart = dispatch?.UseStart,
                    UseEnd = dispatch?.UseEnd,
                    StateText = stateText,
                    RestUntil = restUntil,
                    RestRemainMinutes = restRemainMinutes,
                    Attendance = attendance
                });
            }


            return Ok(ApiResponse<List<DriverStatusDto>>.Ok(result, "今日駕駛狀態查詢成功"));

        }



        #endregion


        #region 今日未完成任務
        //  未完成派工
        [HttpGet("dispatch/uncomplete")]
        public async Task<ActionResult<ApiResponse<List<UncompleteDispatchDto>>>> Uncomplete()
        {
            var today = DateTime.Today;

            var raw = await (
                from d in _db.Dispatches
                join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId into apj
                from ap in apj.DefaultIfEmpty()
                join drv in _db.Drivers on d.DriverId equals drv.DriverId into drvj
                from drv in drvj.DefaultIfEmpty()
                join v in _db.Vehicles on d.VehicleId equals v.VehicleId into vj
                from v in vj.DefaultIfEmpty()
                where a.UseStart.Date == today
                      && a.UseEnd >= DateTime.Now
                      && a.Status != "駁回"
                orderby a.UseStart,
                a.UseEnd,             
                d.DispatchId
                select new
                {
                    d.DispatchId,
                    a.UseStart,
                    a.UseEnd,
                    Route = (a.Origin ?? "") + "-" + (a.Destination ?? ""),
                    a.ApplyReason,
                    ApplicantName = ap != null ? ap.Name : null,  
                    PassengerCount = a.PassengerCount,
                    a.TripType,
                    a.SingleDistance,
                    a.RoundTripDistance,
                    a.SingleDuration,
                    a.RoundTripDuration,
                    a.Status,
                    DispatchStatus = d.DispatchStatus,
                    DriverName = drv != null ? drv.DriverName : null,
                    PlateNo = v != null ? v.PlateNo : null
                })
                .ToListAsync();

            var data = raw.Select(x => new UncompleteDispatchDto(
                x.UseStart.ToString("yyyy-MM-dd"),
                $"{x.UseStart:HH:mm}-{x.UseEnd:HH:mm}",
                x.Route,
                x.ApplyReason,
                x.ApplicantName,
                x.PassengerCount,
                FormatDistance(x.TripType, x.SingleDistance, x.RoundTripDistance),
                FormatDuration(x.TripType, x.SingleDuration, x.RoundTripDuration),
                x.Status,
                x.DispatchStatus,
                x.DriverName,
                x.PlateNo
            )).ToList();


            return ApiResponse<List<UncompleteDispatchDto>>.Ok(data);
        }
        #endregion

        #region 今日待審核申請
        //  待審核申請
        [HttpGet("applications/pending")]
        public async Task<ActionResult<ApiResponse<List<PendingAppDto>>>> PendingApps()
        {
            var today = DateTime.Today;

            var raw = await (
                from a in _db.CarApplications
                where a.Status == "待審核"
                      && a.UseStart.Date == today
                join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId into apj
                from ap in apj.DefaultIfEmpty()
                orderby a.UseStart
                select new
                {
                    a.ApplyId,
                    a.UseStart,
                    a.UseEnd,
                    a.Origin,
                    a.Destination,
                    a.ApplyReason,
                    ApplicantName = ap != null ? ap.Name : null,
                    a.PassengerCount,
                    a.TripType,
                    a.VehicleId,
                    a.SingleDistance,
                    a.RoundTripDistance,
                    a.SingleDuration,
                    a.RoundTripDuration,
                    a.Status
                }
            ).ToListAsync();

            var data = raw.Select(a => new PendingAppDto(
                                  a.ApplyId,
                                  a.UseStart.ToString("yyyy-MM-dd"),
                                  $"{a.UseStart:HH:mm}-{a.UseEnd:HH:mm}",
                                  (a.Origin ?? "") + "-" + (a.Destination ?? ""),
                                  a.ApplyReason,
                                  a.ApplicantName,
                                  a.PassengerCount,
                                  FormatDistance(a.TripType, a.SingleDistance, a.RoundTripDistance),
                                  FormatDuration(a.TripType, a.SingleDuration, a.RoundTripDuration),
                                  a.Status
                              )).ToList();

            return ApiResponse<List<PendingAppDto>>.Ok(data);
        }
            #endregion

        /// <summary>依照單程/來回，格式化距離</summary>
        private static string FormatDistance(string tripType, decimal? single, decimal? round)
        {
            return tripType == "單程"
                ? (single.HasValue ? $"{single.Value} 公里" : "")
                : (round.HasValue ? $"{round.Value} 公里" : "");
        }

        /// <summary>依照單程/來回，格式化時間</summary>
        private static string FormatDuration(string tripType, string? single, string? round)
        {
            return tripType == "單程"
                ? (!string.IsNullOrEmpty(single) ? $"{single} 分鐘" : "")
                : (!string.IsNullOrEmpty(round) ? $"{round} 分鐘" : "");
        }
        //GPS 位置
        // 靜態保存目前每台車的座標
        private static Dictionary<int, (double lat, double lng)> carPositions = new();

        [HttpGet("vehicle-locations")]
        public IActionResult GetAllVehicleLocations()
        {
            var now = DateTime.Now;

            // === Step 1. 找出每台車最後一筆 GPS ===
            var latestTimes = (
                from loc in _db.VehicleLocationLogs
                group loc by loc.VehicleId into g
                select new
                {
                    VehicleId = g.Key,
                    MaxGpsTime = g.Max(x => x.GpsTime)
                }
            );

            // === Step 2. 找出目前正在執勤的派工（含駕駛）===
            var activeAssignments = (
                from dis in _db.Dispatches
                join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                join d in _db.Drivers on dis.DriverId equals d.DriverId
                where dis.VehicleId != null
                      && dis.DriverId != null
                      &&
                      (
                          //  Case 1: 有派工結束時間，用它判斷是否還沒完工
                          (dis.StartTime.HasValue && (!dis.EndTime.HasValue || dis.EndTime > now))
                          ||
                          //  Case 2: 沒 EndTime（尚未完工或未設定），用申請單時間區間判斷
                          (!dis.EndTime.HasValue && a.UseStart <= now && a.UseEnd >= now)
                      )
                select new
                {
                    dis.VehicleId,
                    dis.DriverId,
                    d.DriverName
                }
            ).Distinct().ToList();


            var activeMap = activeAssignments
                .GroupBy(x => x.VehicleId)
                .ToDictionary(
                    g => g.Key,
                    g => new { g.First().DriverId, g.First().DriverName } 
                );

            // === Step 3. GPS + 車牌 + 駕駛 ===
            var latest = (
                from loc in _db.VehicleLocationLogs
                join lt in latestTimes
                    on new { loc.VehicleId, loc.GpsTime } equals new { lt.VehicleId, GpsTime = lt.MaxGpsTime }
                join v in _db.Vehicles on loc.VehicleId equals v.VehicleId
                select new
                {
                    loc.VehicleId,
                    v.PlateNo,
                    loc.Latitude,
                    loc.Longitude,
                    loc.Speed,
                    loc.Heading,
                    loc.GpsTime,
                    IsOnDuty = activeMap.ContainsKey(loc.VehicleId),
                    DriverId = activeMap.ContainsKey(loc.VehicleId)
                        ? activeMap[loc.VehicleId].DriverId
                        : (int?)null,
                    DriverName = activeMap.ContainsKey(loc.VehicleId)
                        ? activeMap[loc.VehicleId].DriverName
                        : null
                }
            ).AsNoTracking().ToList();


            // === Step 4. 無資料時使用模擬 ===
            if (latest.Count > 0)
            {
                Console.WriteLine($"✅ 使用資料庫中的 {latest.Count} 筆車輛定位資料。");
                return Ok(latest);
            }

            Console.WriteLine("⚠️ 資料庫中沒有定位資料，使用模擬資料。");
            return Ok(GenerateMockVehicleLocations());
        }




        // -----------------------------
        // 模擬資料產生器
        // -----------------------------
        private static List<object> GenerateMockVehicleLocations()
        {
            var rand = new Random();

            // 台中起點、大約位置
            const double startLat = 24.147735; // 台中市政府附近
            const double startLng = 120.673648;
            const double endLat = 25.033964;   // 台北101
            const double endLng = 121.564468;

            // 每次往北移動一小段（0.001 度約 100m）
            const double stepLat = (endLat - startLat) / 500; // 分成500段
            const double stepLng = (endLng - startLng) / 500;

            var list = new List<object>();

            for (int id = 1; id <= 5; id++)
            {
                if (!carPositions.ContainsKey(id))
                    carPositions[id] = (startLat + id * 0.001, startLng + id * 0.001);

                var (lat, lng) = carPositions[id];

                // 每次移動一點點（微隨機偏移）
                lat += stepLat + (rand.NextDouble() - 0.5) * 0.0001;
                lng += stepLng + (rand.NextDouble() - 0.5) * 0.0001;

                carPositions[id] = (lat, lng);

                list.Add(new
                {
                    VehicleId = id,
                    Latitude = lat,
                    Longitude = lng,
                    Speed = rand.Next(60, 100),
                    Heading = rand.Next(0, 360),
                    GpsTime = DateTime.Now
                });
            }

            return list;
        }








    }
}
