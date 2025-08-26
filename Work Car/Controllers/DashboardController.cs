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

        // ① 卡片數字
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

        // ② 今日排班（帶目前/最近一筆今天的派工與申請資訊）
        [HttpGet("schedule/today")]
        public async Task<IActionResult> TodaySchedule()
        {
            var today = DateTime.Today;

            var list = await _db.Schedules
                .Where(s => s.WorkDate == today)
                .Select(s => new
                {
                    scheduleId = s.ScheduleId,
                    shift = s.Shift,
                    driverId = s.DriverId,
                    driverName = _db.Drivers
                        .Where(d => d.DriverId == s.DriverId)
                        .Select(d => d.DriverName)
                        .FirstOrDefault(),

                    // 是否今天有任何派工
                    hasDispatch = _db.Dispatches.Any(dis =>
                        dis.DriverId == s.DriverId &&
                        dis.StartTime.HasValue &&
                        dis.StartTime.Value.Date == today),

                    // 取今天最早的一筆派工（你也可改成「進行中優先」的選法）
                    startTime = _db.Dispatches
                        .Where(dis => dis.DriverId == s.DriverId &&
                                      dis.StartTime.HasValue &&
                                      dis.StartTime.Value.Date == today)
                        .OrderBy(dis => dis.StartTime)
                        .Select(dis => dis.StartTime)
                        .FirstOrDefault(),

                    endTime = _db.Dispatches
                        .Where(dis => dis.DriverId == s.DriverId &&
                                      dis.StartTime.HasValue &&
                                      dis.StartTime.Value.Date == today)
                        .OrderBy(dis => dis.StartTime)
                        .Select(dis => dis.EndTime)
                        .FirstOrDefault(),

                    // 路線（從該派工對應的申請單拿）
                    route = (
                        from dis in _db.Dispatches
                        where dis.DriverId == s.DriverId
                              && dis.StartTime.HasValue
                              && dis.StartTime.Value.Date == today
                        orderby dis.StartTime
                        join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                        select (a.Origin ?? "") + "-" + (a.Destination ?? "")
                    ).FirstOrDefault(),

                    // 申請人/單位/人數
                    applicantName = (
                        from dis in _db.Dispatches
                        where dis.DriverId == s.DriverId
                              && dis.StartTime.HasValue
                              && dis.StartTime.Value.Date == today
                        orderby dis.StartTime
                        join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                        select a.ApplicantName
                    ).FirstOrDefault(),

                    applicantDept = (
                        from dis in _db.Dispatches
                        where dis.DriverId == s.DriverId
                              && dis.StartTime.HasValue
                              && dis.StartTime.Value.Date == today
                        orderby dis.StartTime
                        join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                        select a.ApplicantDept
                    ).FirstOrDefault(),

                    passengerCount = (
                        from dis in _db.Dispatches
                        where dis.DriverId == s.DriverId
                              && dis.StartTime.HasValue
                              && dis.StartTime.Value.Date == today
                        orderby dis.StartTime
                        join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                        select a.PassengerCount
                    ).FirstOrDefault(),

                    // 車牌
                    plateNo = (
                        from dis in _db.Dispatches
                        where dis.DriverId == s.DriverId
                              && dis.StartTime.HasValue
                              && dis.StartTime.Value.Date == today
                        orderby dis.StartTime
                        join v in _db.Vehicles on dis.VehicleId equals v.VehicleId
                        select v.PlateNo
                    ).FirstOrDefault(),

                    // 里程（你的欄位是字串 SingleDistance / RoundTripDistance，就直接回字串）
                    tripDistance = (
                        from dis in _db.Dispatches
                        where dis.DriverId == s.DriverId
                              && dis.StartTime.HasValue
                              && dis.StartTime.Value.Date == today
                        orderby dis.StartTime
                        join a in _db.CarApplications on dis.ApplyId equals a.ApplyId
                        select (a.TripType == "單程"
                                ? (string.IsNullOrEmpty(a.SingleDistance) ? "" : a.SingleDistance )
                                : (string.IsNullOrEmpty(a.RoundTripDistance) ? "" : a.RoundTripDistance ))
                    ).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(list);
        }


        // ③ 未完成派工
        [HttpGet("dispatch/uncomplete")]
        public async Task<IActionResult> Uncomplete()
        {
            var today = DateTime.Today;
            var raw = await (
                from d in _db.Dispatches
                join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                join drv in _db.Drivers on d.DriverId equals drv.DriverId into drvj
                from drv in drvj.DefaultIfEmpty()
                join v in _db.Vehicles on d.VehicleId equals v.VehicleId into vj
                from v in vj.DefaultIfEmpty()
                where (d.DispatchStatus != "已完成"
                      || d.DriverId == null || d.VehicleId == null || d.DispatchStatus == "待派車" )&& a.UseEnd.Date == today
                orderby a.UseStart
                select new
                {
                    d.DispatchId,
                    a.UseStart,
                    a.UseEnd,
                    Route = (a.Origin ?? "") + "-" + (a.Destination ?? ""),
                    a.ApplyReason,
                    a.ApplicantName,
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

        // ④ 待審核申請
        [HttpGet("applications/pending")]
        public async Task<IActionResult> PendingApps()
        {
            var today = DateTime.Today;
            var raw = await _db.CarApplications
                .Where(a => (a.Status == "待審核" || a.Status == "審核中")
                && a.UseStart.Date == today)
                .OrderBy(a => a.UseStart)
                .ToListAsync();

            var data = raw.Select(a => new
            {
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
