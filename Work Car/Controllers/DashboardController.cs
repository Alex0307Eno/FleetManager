using Cars.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers
{
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
                join dis in _db.Dispatches on s.DriverId equals dis.DriverId into disGroup
                from dis in disGroup
                    .Where(x => x.StartTime.HasValue && x.StartTime.Value.Date == today)
                    .OrderBy(x => x.StartTime)
                    .DefaultIfEmpty()
                join a in _db.CarApplications on dis.ApplyId equals a.ApplyId into aa
                from a in aa.DefaultIfEmpty()
                join v in _db.Vehicles on dis.VehicleId equals v.VehicleId into vv
                from v in vv.DefaultIfEmpty()
                    // 🔗 這裡多加 Applicants join
                join ap in _db.Applicants on a.ApplicantId equals ap.ApplicantId into appGroup
                from ap in appGroup.DefaultIfEmpty()
                orderby s.Shift, dis.StartTime
                select new
                {
                    scheduleId = s.ScheduleId,
                    shift = s.Shift,   // AM/PM
                    driverId = s.DriverId,
                    driverName = d.DriverName,
                    hasDispatch = dis != null,
                    startTime = dis.StartTime,
                    endTime = dis.EndTime,
                    route = dis != null ? (a.Origin ?? "") + "-" + (a.Destination ?? "") : "",
                    applicantName = ap != null ? ap.Name : null,   // ✅ 從 Applicants 取
                    applicantDept = ap != null ? ap.Dept : null,   // ✅ 從 Applicants 取
                    passengerCount = a.PassengerCount,
                    plateNo = v.PlateNo,
                    tripDistance = a != null
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
                    ).FirstOrDefault()
                })
                .ToListAsync();

            // 統一處理狀態文字
            var result = drivers.Select(d => new {
                d.driverId,
                d.driverName,
                d.shift,
                d.plateNo,
                d.applicantDept,
                d.applicantName,
                d.passengerCount,
                d.startTime,
                d.endTime,
                stateText = d.isOnDuty ? "執勤中" : "待命中"
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
                      && a.UseEnd.Date == today
                orderby a.UseStart
                select new
                {
                    d.DispatchId,
                    a.UseStart,
                    a.UseEnd,
                    Route = (a.Origin ?? "") + "-" + (a.Destination ?? ""),
                    a.ApplyReason,
                    ApplicantName = ap != null ? ap.Name : null,   // ✅ 從 Applicants.Name
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
                    ApplicantName = ap != null ? ap.Name : null,   // ✅ 改用 Applicants.Name
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
