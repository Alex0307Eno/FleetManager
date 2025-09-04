using Cars.Data;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public DashboardController(ApplicationDbContext db) { _db = db; }

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

            var list = await (
                from s in _db.Schedules
                where s.WorkDate == today
                join d in _db.Drivers on s.DriverId equals d.DriverId
                join dis0 in _db.Dispatches on s.DriverId equals dis0.DriverId into disGroup
                from dis in disGroup
                    .Where(x => x.StartTime.HasValue && x.StartTime.Value.Date == today)
                    .OrderBy(x => x.StartTime)
                    .DefaultIfEmpty()
                join a0 in _db.CarApplications on dis.ApplyId equals a0.ApplyId into aa
                from a in aa.DefaultIfEmpty()
                join v0 in _db.Vehicles on dis.VehicleId equals v0.VehicleId into vv
                from v in vv.DefaultIfEmpty()
                join ap0 in _db.Applicants on a.ApplicantId equals ap0.ApplicantId into appGroup
                from ap in appGroup.DefaultIfEmpty()
                orderby s.Shift, dis.StartTime
                select new
                {
                    scheduleId = s.ScheduleId,
                    shift = s.Shift,
                    driverId = s.DriverId,
                    driverName = d.DriverName,

                    hasDispatch = dis != null,
                    startTime = (DateTime?)(dis != null ? dis.StartTime : null),
                    endTime = (DateTime?)(dis != null ? dis.EndTime : null),

                    // ★ a 可能為 null，所以用 a != null 判斷
                    route = (a != null) ? ((a.Origin ?? "") + "-" + (a.Destination ?? "")) : "",

                    applicantName = ap != null ? ap.Name : null,
                    applicantDept = ap != null ? ap.Dept : null,

                    // ★ 避免把 null 投到非可空型別
                    passengerCount = (a != null ? a.PassengerCount : 0),

                    plateNo = v != null ? v.PlateNo : null,

                    tripDistance = (a != null)
                        ? (a.TripType == "單程" ? a.SingleDistance : a.RoundTripDistance)
                        : ""
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

            var drivers = await _db.Drivers
                .Select(d => new
                {
                    driverId = d.DriverId,
                    driverName = d.DriverName,

                    // 班表
                   shift = _db.Schedules
                        .Where(s => s.DriverId == d.DriverId && s.WorkDate == today)
                        .Select(s => s.Shift)
                        .FirstOrDefault(),

                    // 是否正在執勤
                    isOnDuty = _db.Dispatches.Any(dis =>
                        dis.DriverId == d.DriverId &&
                        dis.StartTime.HasValue &&
                        dis.EndTime.HasValue &&
                        dis.StartTime.Value <= now &&
                        dis.EndTime.Value >= now

                    ),

                    // 當前派工的車牌
                    plateNo = (
                        from dis in _db.Dispatches
                        where dis.DriverId == d.DriverId
                              && dis.StartTime.HasValue && dis.EndTime.HasValue
                              && dis.StartTime.Value <= now
                              && dis.EndTime.Value >= now
                        join v in _db.Vehicles on dis.VehicleId equals v.VehicleId
                        select v.PlateNo
                    ).FirstOrDefault(),

                    // 🔑 申請人部門
                    applicantDept = (
                        from dis in _db.Dispatches
                        where dis.DriverId == d.DriverId
                              && dis.StartTime.HasValue && dis.EndTime.HasValue
                              && dis.StartTime.Value <= now
                              && dis.EndTime.Value >= now
                        join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                        join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId
                        select ap.Dept
                    ).FirstOrDefault(),

                    // 🔑 申請人姓名
                    applicantName = (
                        from dis in _db.Dispatches
                        where dis.DriverId == d.DriverId
                              && dis.StartTime.HasValue && dis.EndTime.HasValue
                              && dis.StartTime.Value <= now
                              && dis.EndTime.Value >= now
                        join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                        join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId
                        select ap.Name
                    ).FirstOrDefault(),

                    passengerCount = (
                        from dis in _db.Dispatches
                        where dis.DriverId == d.DriverId
                              && dis.StartTime.HasValue && dis.EndTime.HasValue
                              && dis.StartTime.Value <= now
                              && dis.EndTime.Value >= now
                        join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                        select a.PassengerCount
                    ).FirstOrDefault(),

                    // 當前派工的時間
                    startTime = (
                        from dis in _db.Dispatches
                        where dis.DriverId == d.DriverId
                              && dis.StartTime.HasValue && dis.EndTime.HasValue
                              && dis.StartTime.Value <= now
                              && dis.EndTime.Value >= now
                        select dis.StartTime
                    ).FirstOrDefault(),

                    endTime = (
                        from dis in _db.Dispatches
                        where dis.DriverId == d.DriverId
                              && dis.StartTime.HasValue && dis.EndTime.HasValue
                              && dis.StartTime.Value <= now
                              && dis.EndTime.Value >= now
                        select dis.EndTime
                    ).FirstOrDefault(),
                    // 最近一次的長途派工結束時間
                    lastLongEnd = _db.Dispatches
                .Where(x => x.DriverId == d.DriverId && x.IsLongTrip && x.EndTime != null)
                .OrderByDescending(x => x.EndTime)
                .Select(x => x.EndTime)
                .FirstOrDefault()
                })
                .ToListAsync();

            var result = drivers.Select(d =>
            {
                // 判斷是否休息中：最近長差的 EndTime 在 1 小時內
                bool isResting = false;
                DateTime? restUntil = null;
                int? restRemainMinutes = null;

                if (d.lastLongEnd.HasValue)
                {
                    var until = d.lastLongEnd.Value.AddHours(1);
                    if (DateTime.Now < until)
                    {
                        isResting = true;
                        restUntil = until;
                        restRemainMinutes = (int)Math.Ceiling((until - DateTime.Now).TotalMinutes);
                    }
                }

                var stateText = d.isOnDuty ? "執勤中" : (isResting ? "休息中" : "待命中");

                return new
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
                    stateText,
                    restUntil,            // 前端可顯示「休息到 HH:mm」
                    restRemainMinutes     // 前端可顯示「(剩 X 分鐘)」
                };
            });

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
                orderby a.UseStart
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
                    ? (!string.IsNullOrEmpty(x.SingleDistance) ? x.SingleDistance + " 公里" : "")
                    : (!string.IsNullOrEmpty(x.RoundTripDistance) ? x.RoundTripDistance + " 公里" : ""),

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
                    ? (!string.IsNullOrEmpty(a.SingleDistance) ? a.SingleDistance + " 公里" : "")
                    : (!string.IsNullOrEmpty(a.RoundTripDistance) ? a.RoundTripDistance + " 公里" : ""),

                tripDuration = a.TripType == "單程"
                    ? (!string.IsNullOrEmpty(a.SingleDuration) ? a.SingleDuration + " 分鐘" : "")
                    : (!string.IsNullOrEmpty(a.RoundTripDuration) ? a.RoundTripDuration + " 分鐘" : ""),

                status = a.Status
            });

            return Ok(data);
        }
    }
}
