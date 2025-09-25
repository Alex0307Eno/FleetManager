using Cars.Data;
using Cars.Dtos;
using Cars.Features.DashBoard;
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

            var list = await (
                from s in _db.Schedules
                where s.WorkDate == today
                join d in _db.Drivers on s.DriverId equals d.DriverId

                // 左連代理
                join dg0 in _db.DriverDelegations
                     .Where(g => g.StartDate <= today && today <= g.EndDate)
                     on s.DriverId equals dg0.PrincipalDriverId into dgs
                from dg in dgs.DefaultIfEmpty()

                join agent0 in _db.Drivers on dg.AgentDriverId equals agent0.DriverId into ags
                from agent in ags.DefaultIfEmpty()

                    // 若請假且有代理，顯示代理；否則顯示原駕駛
                let showDriverId = (dg != null && agent != null && s.IsPresent == false) ? agent.DriverId : d.DriverId
                let showDriverName = (dg != null && agent != null && s.IsPresent == false) ? (agent.DriverName + " (代)") : d.DriverName

                // 展開今日該駕駛的所有派工（允許沒有派工）
                from dis in _db.Dispatches
                    .Where(x => x.DriverId == showDriverId
                                && x.StartTime.HasValue
                                && x.StartTime.Value.Year == today.Year
                                && x.StartTime.Value.Month == today.Month
                                && x.StartTime.Value.Day == today.Day)
                    .DefaultIfEmpty()

                join a0 in _db.CarApplications on dis.ApplyId equals a0.ApplyId into aa
                from a in aa.DefaultIfEmpty()

                join v0 in _db.Vehicles on dis.VehicleId equals v0.VehicleId into vv
                from v in vv.DefaultIfEmpty()

                join ap0 in _db.Applicants on a.ApplicantId equals ap0.ApplicantId into appGroup
                from ap in appGroup.DefaultIfEmpty()

                    // 排序 key（不能用 ?.，用三元運算子）
                let sortTime =
                    (dis != null && dis.StartTime.HasValue)
                        ? dis.StartTime.Value
                        : (s.Shift == "早" ? today.AddHours(8)
                         : s.Shift == "中" ? today.AddHours(12)
                         : s.Shift == "晚" ? today.AddHours(18)
                         : today.AddHours(23).AddMinutes(59))

                orderby sortTime, (dis != null ? dis.DispatchId : int.MaxValue)

                select new TodayScheduleDto
                {
                    ScheduleId = s.ScheduleId,
                    Shift = s.Shift,
                    DriverId = showDriverId,
                    DriverName = showDriverName,
                    HasDispatch = (dis != null),
                    StartTime = (dis != null ? dis.StartTime : (DateTime?)null),
                    EndTime = (dis != null ? dis.EndTime : (DateTime?)null),
                    Route = (a != null ? ((a.Origin ?? "") + "-" + (a.Destination ?? "")) : ""),
                    ApplicantName = (ap != null ? ap.Name : null),
                    ApplicantDept = (ap != null ? ap.Dept : null),
                    PassengerCount = (a != null ? a.PassengerCount : 0),
                    PlateNo = (v != null ? v.PlateNo : null),
                    TripDistance = (a != null
                                      ? (a.TripType == "單程"
                                          ? (a.SingleDistance ?? 0)
                                          : (a.RoundTripDistance ?? 0))
                                      : 0),
                    Attendance = (s.IsPresent ? "正常" : "請假")
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

            // === Step 1. 駕駛基本資料 + 今日班別 ===
            var baseDrivers = await (
                from d in _db.Drivers.Where(d => d.IsAgent == false)
                join s in _db.Schedules.Where(s => s.WorkDate == today) on d.DriverId equals s.DriverId into sg
                from s in sg.DefaultIfEmpty()
                select new
                {
                    d.DriverId,
                    d.DriverName,
                    Shift = s != null ? s.Shift : null,
                    IsPresent = s != null && s.IsPresent
                }
            ).AsNoTracking().ToListAsync();

            // === Step 2. 當前派工（正在執勤的那張，挑最貼近現在的一筆） ===
            var dispatchesNow = await (
                from dis in _db.Dispatches
                where dis.StartTime.HasValue && dis.StartTime.Value <= now &&
                      (!dis.EndTime.HasValue || dis.EndTime.Value >= now)
                join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId
                join v in _db.Vehicles on dis.VehicleId equals v.VehicleId into vv
                from v in vv.DefaultIfEmpty()
                where dis.DriverId != null
                select new
                {
                    dis.DriverId,
                    dis.StartTime,
                    dis.EndTime,
                    PlateNo = v != null ? v.PlateNo : null,
                    Dept = ap.Dept,
                    Name = ap.Name,
                    a.PassengerCount
                }
            )
            .OrderByDescending(x => x.StartTime)
            .ThenBy(x => x.EndTime)
            .AsNoTracking()
            .ToListAsync();

            var dispatchByDriver = dispatchesNow
                .GroupBy(x => x.DriverId!.Value)
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

            // === Step 4. 查每個駕駛今天最後一筆長差，用來計算休息中 ===
            var longTripEnds = await (
                from dis in _db.Dispatches
                where dis.IsLongTrip && dis.EndTime.HasValue && dis.EndTime.Value <= now && dis.EndTime.Value.Date == today
                select new { dis.DriverId, dis.EndTime }
            ).AsNoTracking().ToListAsync();

            var lastLongEndByDriver = longTripEnds
                .GroupBy(x => x.DriverId!.Value)
                .ToDictionary(g => g.Key, g => g.Max(z => z.EndTime!.Value));

            // === Step 5. 組合結果 ===
            var result = new List<DriverStatusDto>();

            foreach (var d in baseDrivers)
            {
                bool isAgenting = false;
                var driverId = d.DriverId;
                var driverName = d.DriverName;

                // 缺勤 → 代理人頂替
                if (!d.IsPresent && delegMap.TryGetValue(d.DriverId, out var proxyAgent))
                {
                    isAgenting = true;
                    driverId = proxyAgent.DriverId;
                    driverName = $"{proxyAgent.DriverName}(代)";
                }

                var dispatch = dispatchByDriver.ContainsKey(driverId)
                    ? dispatchByDriver[driverId]
                    : null;

                // === 狀態判斷 ===
                bool isResting = false;
                DateTime? restUntil = null;
                int? restRemainMinutes = null;
                string stateText;

                // 有派工 → 執勤中
                if (dispatch != null)
                {
                    stateText = "執勤中";
                }
                else
                {
                    // 沒派工 → 看是否剛跑完長差
                    if (lastLongEndByDriver.TryGetValue(driverId, out var lastEnd))
                    {
                        var until = lastEnd.AddHours(1);
                        if (now < until)
                        {
                            isResting = true;
                            restUntil = until;
                            restRemainMinutes = (int)Math.Ceiling((until - now).TotalMinutes);
                        }
                    }
                    stateText = isResting ? "休息中" : "待命中";
                }

                result.Add(new DriverStatusDto
                {
                    DriverId = driverId,
                    DriverName = driverName ?? "-",
                    Shift = d.Shift,
                    PlateNo = dispatch?.PlateNo,
                    ApplicantDept = dispatch?.Dept,
                    ApplicantName = dispatch?.Name,
                    PassengerCount = dispatch?.PassengerCount,
                    StartTime = dispatch?.StartTime,
                    EndTime = dispatch?.EndTime,
                    StateText = stateText,
                    Attendance = isAgenting ? $"請假({d.DriverName ?? "-"})" : (d.IsPresent ? "正常" : "請假"),
                    RestUntil = restUntil,
                    RestRemainMinutes = restRemainMinutes
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
                     && (d.DispatchStatus != "已完成" || d.DriverId == null || d.VehicleId == null)


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
                where (a.Status == "待審核" || a.Status == "審核中")
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
            #endregion
        }

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
    }
}
