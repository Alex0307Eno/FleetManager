using Cars.Data;
using Cars.Models;
using Cars.Services;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Cars.Controllers
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

            return Ok(new
            {
                scheduleTodayCount,
                uncompleteCount,
                pendingReviewCount
            });
        }

        //  今日排班
        [HttpGet("schedule/today")]
        public async Task<IActionResult> TodaySchedule()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            var list = await (
                from s in _db.Schedules
                where s.WorkDate == today
                join d in _db.Drivers on s.DriverId equals d.DriverId

                // 代理對應（同日有效）
                join dg0 in _db.DriverDelegations
                     .Where(g => g.StartDate.Date <= today && today <= g.EndDate.Date)
                     on s.DriverId equals dg0.PrincipalDriverId into dgs
                from dg in dgs.DefaultIfEmpty()
                join agent0 in _db.Drivers on dg.AgentDriverId equals agent0.DriverId into ags
                from agent in ags.DefaultIfEmpty()

                    // 顯示駕駛：有代理就換代理人
                let showDriverId = (dg != null && agent != null) ? agent.DriverId : d.DriverId
                let showDriverName = (dg != null && agent != null) ? (agent.DriverName + " (代)") : d.DriverName

                // ★ 這裡改成展開所有今日派工，不要 FirstOrDefault()
                from dis in _db.Dispatches
                    .Where(x => x.DriverId == showDriverId && x.StartTime.HasValue && x.StartTime.Value.Date == today)
                    .DefaultIfEmpty()

                join a0 in _db.CarApplications on dis.ApplyId equals a0.ApplyId into aa
                from a in aa.DefaultIfEmpty()
                join v0 in _db.Vehicles on dis.VehicleId equals v0.VehicleId into vv
                from v in vv.DefaultIfEmpty()
                join ap0 in _db.Applicants on a.ApplicantId equals ap0.ApplicantId into appGroup
                from ap in appGroup.DefaultIfEmpty()

                    // 排序：有任務就用 StartTime，否則用班別預設時間
                let sortTime =
                    (dis != null && dis.StartTime.HasValue)
                        ? dis.StartTime.Value
                        : (s.Shift == "早" ? today.AddHours(8)
                         : s.Shift == "中" ? today.AddHours(12)
                         : s.Shift == "晚" ? today.AddHours(18)
                         : today.AddHours(23).AddMinutes(59))

                orderby sortTime, (dis != null ? dis.DispatchId : int.MaxValue)

                select new
                {
                    scheduleId = s.ScheduleId,
                    shift = s.Shift,
                    driverId = showDriverId,
                    driverName = showDriverName,
                    hasDispatch = dis != null,
                    startTime = dis != null ? dis.StartTime : null,
                    endTime = dis != null ? dis.EndTime : null,
                    route = (a != null ? ((a.Origin ?? "") + "-" + (a.Destination ?? "")) : ""),
                    applicantName = ap != null ? ap.Name : null,
                    applicantDept = ap != null ? ap.Dept : null,
                    passengerCount = a != null ? a.PassengerCount : 0,
                    plateNo = v != null ? v.PlateNo : null,
                    tripDistance = (a != null
                    ? (a.TripType == "單程" ? (a.SingleDistance ?? 0) : (a.RoundTripDistance ?? 0))
                    : 0),
                    attendance = s.IsPresent ? "正常" : "請假"   
                }
            ).ToListAsync();

            return Ok(list);
        }

        //駕駛目前狀態
        [HttpGet("drivers/today-status")]
        public async Task<IActionResult> DriversTodayStatus()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            // 1) 司機即時狀態
            var drivers = await _db.Drivers
                .Select(d => new
                {
                    driverId = d.DriverId,
                    driverName = d.DriverName,
                    shift = _db.Schedules.Where(s => s.DriverId == d.DriverId && s.WorkDate == today)
                                         .Select(s => s.Shift).FirstOrDefault(),
                    isPresent = _db.Schedules.Any(s => s.DriverId == d.DriverId &&
                                                       s.WorkDate == today &&
                                                       s.IsPresent == true),
                    isOnDuty = _db.Dispatches.Any(dis => dis.DriverId == d.DriverId &&
                                     dis.StartTime.HasValue &&
                                     dis.StartTime.Value <= now &&
                                     (!dis.EndTime.HasValue || dis.EndTime.Value >= now)),

                    plateNo = (from dis in _db.Dispatches
                               where dis.DriverId == d.DriverId &&
                                     dis.StartTime.HasValue && dis.EndTime.HasValue &&
                                     dis.StartTime.Value <= now && dis.EndTime.Value >= now
                               join v in _db.Vehicles on dis.VehicleId equals v.VehicleId
                               select v.PlateNo).FirstOrDefault(),
                    applicantDept = (from dis in _db.Dispatches
                                     where dis.DriverId == d.DriverId &&
                                           dis.StartTime.HasValue && dis.EndTime.HasValue &&
                                           dis.StartTime.Value <= now && dis.EndTime.Value >= now
                                     join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                                     join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId
                                     select ap.Dept).FirstOrDefault(),
                    applicantName = (from dis in _db.Dispatches
                                     where dis.DriverId == d.DriverId &&
                                           dis.StartTime.HasValue && dis.EndTime.HasValue &&
                                           dis.StartTime.Value <= now && dis.EndTime.Value >= now
                                     join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                                     join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId
                                     select ap.Name).FirstOrDefault(),
                    passengerCount = (from dis in _db.Dispatches
                                      where dis.DriverId == d.DriverId &&
                                            dis.StartTime.HasValue && dis.EndTime.HasValue &&
                                            dis.StartTime.Value <= now && dis.EndTime.Value >= now
                                      join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                                      select a.PassengerCount).FirstOrDefault(),
                    startTime = (from dis in _db.Dispatches
                                 where dis.DriverId == d.DriverId &&
                                       dis.StartTime.HasValue && dis.EndTime.HasValue &&
                                       dis.StartTime.Value <= now && dis.EndTime.Value >= now
                                 select dis.StartTime).FirstOrDefault(),
                    endTime = (from dis in _db.Dispatches
                               where dis.DriverId == d.DriverId &&
                                     dis.StartTime.HasValue && dis.EndTime.HasValue &&
                                     dis.StartTime.Value <= now && dis.EndTime.Value >= now
                               select dis.EndTime).FirstOrDefault(),
                    lastLongEnd = _db.Dispatches
                    .Where(x => x.DriverId == d.DriverId
                             && x.IsLongTrip
                             && x.EndTime.HasValue
                             && x.EndTime.Value.Date == today
                             && x.EndTime.Value <= now)  // 只取已經結束的
                    .OrderByDescending(x => x.EndTime)
                    .Select(x => x.EndTime)
                    .FirstOrDefault(),

                    isAgent = d.IsAgent
                })
                .ToListAsync();

            // 2) 代理映射（保留你原本的「同一被代理人取最新」）
            var delegs = await (
                from dg in _db.DriverDelegations.AsNoTracking()
                where dg.StartDate.Date <= today && today <= dg.EndDate.Date
                join agent in _db.Drivers on dg.AgentDriverId equals agent.DriverId
                select new
                {
                    dg.PrincipalDriverId,
                    AgentDriverId = agent.DriverId,
                    AgentName = agent.DriverName,
                    agent.IsAgent,
                    dg.CreatedAt
                }
            ).ToListAsync();

            var delegMap = delegs
                .Where(x => x.IsAgent)
                .GroupBy(x => x.PrincipalDriverId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(z => z.CreatedAt).First());

            // 找出今天缺勤的被代理者
            var absentPrincipals = await _db.Schedules
                .Where(s => s.WorkDate == today && s.IsPresent == false)
                .Select(s => s.DriverId)
                .Distinct()
                .ToListAsync();

            // 今天「真的在頂替」的代理人id集合
            var coveringAgentIds = delegMap
                .Where(kv => absentPrincipals.Contains(kv.Key))
                .Select(kv => kv.Value.AgentDriverId)
                .ToHashSet();

            // 建字典方便查代理人的即時資料
            var byId = drivers.ToDictionary(x => x.driverId, x => x);

            // 3) 組結果：缺勤的「被代理者」→ 用代理人的即時資料；避免重複顯示代理人
            var result = new List<object>();
            var usedAgents = new HashSet<int>();

            foreach (var d in drivers)
            {
                // 若此人是「被代理者」且缺勤，則用代理人取代顯示
                if (!d.isPresent && delegMap.TryGetValue(d.driverId, out var proxy) && byId.TryGetValue(proxy.AgentDriverId, out var agentInfo))
                {
                    // 判斷代理人的休息狀態（沿用你原本邏輯）
                    bool isResting = false;
                    DateTime? restUntil = null;
                    int? restRemainMinutes = null;

                    if (agentInfo.lastLongEnd.HasValue)
                    {
                        var until = agentInfo.lastLongEnd.Value.AddHours(1);
                        if (now < until)
                        {
                            isResting = true;
                            restUntil = until;
                            restRemainMinutes = (int)Math.Ceiling((until - now).TotalMinutes);
                        }
                    }

                    var stateText = agentInfo.isOnDuty ? "執勤中" : (isResting ? "休息中" : "待命中");

                    result.Add(new
                    {
                        driverId = agentInfo.driverId,
                        driverName = $"{proxy.AgentName}(代)",
                        shift = d.shift, // 用被代理者的班別呈現
                        plateNo = agentInfo.plateNo,
                        applicantDept = agentInfo.applicantDept,
                        applicantName = agentInfo.applicantName,
                        passengerCount = agentInfo.passengerCount,
                        startTime = agentInfo.startTime,
                        endTime = agentInfo.endTime,
                        stateText,
                        restUntil,
                        restRemainMinutes,
                        attendance = $"請假({d.driverName})"
                    });

                    usedAgents.Add(agentInfo.driverId);
                    continue; 
                }
                if (d.isAgent && !d.isOnDuty && !coveringAgentIds.Contains(d.driverId))
                    continue;

                // 否則原樣顯示當事人（避免把已用過的代理人再顯示一次）
                if (usedAgents.Contains(d.driverId)) continue;

                bool isResting2 = false;
                DateTime? restUntil2 = null;
                int? restRemainMinutes2 = null;

                if (d.lastLongEnd.HasValue)
                {
                    var until2 = d.lastLongEnd.Value.AddHours(1);
                    if (now < until2)
                    {
                        isResting2 = true;
                        restUntil2 = until2;
                        restRemainMinutes2 = (int)Math.Ceiling((until2 - now).TotalMinutes);
                    }
                }

                var stateText2 = d.isOnDuty ? "執勤中" : (isResting2 ? "休息中" : "待命中");
                var attendance2 = d.isPresent ? "正常" : "請假";

                result.Add(new
                {
                    d.driverId,
                    d.driverName,
                    d.shift,
                    d.plateNo,
                    d.applicantDept,
                    d.applicantName,
                    d.passengerCount,
                    d.startTime,
                    d.endTime,
                    stateText = stateText2,
                    restUntil = restUntil2,
                    restRemainMinutes = restRemainMinutes2,
                    attendance = attendance2
                });
            }

            return Ok(result);

        }


        //駕駛目前狀態(休息中)
        [HttpGet("vehicles/today-status")]
        public async Task<IActionResult> VehiclesTodayStatus()
        {
            var now = DateTime.Now;

            var list = await _db.Vehicles
                .Select(v => new
                {
                    v.VehicleId,
                    v.PlateNo,
                    v.Status, // 原車況（可用/維修中…）

                    // 最近一筆派工（不限今日）：拿來判斷是否剛從長差回來
                    last = _db.Dispatches
                        .Where(d => d.VehicleId == v.VehicleId && d.EndTime != null)
                        .OrderByDescending(d => d.EndTime)
                        .Select(d => new { d.EndTime, d.IsLongTrip })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var result = list.Select(x =>
            {
                var vehicleState = x.Status; // 仍保留原車況
                bool isResting = false;
                DateTime? restUntil = null;
                int? restRemainMinutes = null;

                if (x.last != null && x.last.IsLongTrip && x.last.EndTime.HasValue)
                {
                    var until = x.last.EndTime.Value.AddHours(1);
                    if (now < until)
                    {
                        isResting = true;
                        restUntil = until;
                        restRemainMinutes = (int)Math.Ceiling((until - now).TotalMinutes);
                    }
                }

                // 顯示專用狀態字（不覆蓋原車況）：休息中 / 執勤中 / 待命中
                var uiState = isResting ? "休息中" : "待命中";

                return new
                {
                    x.VehicleId,
                    x.PlateNo,
                    vehicleState = x.Status, // 原系統車況
                    uiState,                 // 儀表板顯示用狀態
                    restUntil,
                    restRemainMinutes
                };
            });

            return Ok(result);
        }


        //  未完成派工
        [HttpGet("dispatch/uncomplete")]
        public async Task<IActionResult> Uncomplete()
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
                where (d.DispatchStatus != "已完成"
                       || d.DriverId == null || d.VehicleId == null || d.DispatchStatus == "待派車")
                      && a.UseEnd.Date == today && a.UseEnd > DateTime.Now
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

            var data = raw.Select(x => new
            {
                useDate = x.UseStart.ToString("yyyy-MM-dd"),
                useTime = $"{x.UseStart:HH:mm}-{x.UseEnd:HH:mm}",
                route = x.Route,
                applyReason = x.ApplyReason,
                applicantName = x.ApplicantName,
                passengerCount = x.PassengerCount,

                tripDistance = x.TripType == "單程"
                ? (x.SingleDistance.HasValue ? x.SingleDistance.Value + " 公里" : "")
                : (x.RoundTripDistance.HasValue ? x.RoundTripDistance.Value + " 公里" : ""),

                tripDuration = x.TripType == "單程"
                    ? (!string.IsNullOrEmpty(x.SingleDuration) ? x.SingleDuration + " 分鐘" : "")
                    : (!string.IsNullOrEmpty(x.RoundTripDuration) ? x.RoundTripDuration + " 分鐘" : ""),

                status = x.Status,
                dispatchStatus = x.DispatchStatus,
                driverName = x.DriverName,
                plateNo = x.PlateNo
            });

            return Ok(data);
        }

        //  待審核申請
        [HttpGet("applications/pending")]
        public async Task<IActionResult> PendingApps()
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

            var data = raw.Select(a => new
            {
                applyId = a.ApplyId,
                useDate = a.UseStart.ToString("yyyy-MM-dd"),
                useTime = $"{a.UseStart:HH:mm}-{a.UseEnd:HH:mm}",
                route = (a.Origin ?? "") + "-" + (a.Destination ?? ""),
                applyReason = a.ApplyReason,
                applicantName = a.ApplicantName,
                passengerCount = a.PassengerCount,

                tripDistance = a.TripType == "單程"
                ? (a.SingleDistance.HasValue ? a.SingleDistance.Value + " 公里" : "")
                : (a.RoundTripDistance.HasValue ? a.RoundTripDistance.Value + " 公里" : ""),

                tripDuration = a.TripType == "單程"
                    ? (!string.IsNullOrEmpty(a.SingleDuration) ? a.SingleDuration + " 分鐘" : "")
                    : (!string.IsNullOrEmpty(a.RoundTripDuration) ? a.RoundTripDuration + " 分鐘" : ""),

                status = a.Status
            });

            return Ok(data);
        }
    }
}
