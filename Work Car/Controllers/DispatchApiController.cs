using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers.Api
{
    [ApiController]
    [Route("api/dispatch")]
    public class DispatchApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public DispatchApiController(ApplicationDbContext db) => _db = db;

        [HttpGet("records")]
        public async Task<ActionResult<IEnumerable<RecordDto>>> GetRecords(
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] string? driver,
            [FromQuery] string? applicant,
            [FromQuery] string? plate)
        {
            var q =
         from d in _db.Dispatches.AsNoTracking()
         join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId
         join r in _db.Drivers.AsNoTracking() on d.DriverId equals r.DriverId
         join a in _db.CarApplications.AsNoTracking() on d.ApplyId equals a.ApplyId
         select new
         {
             d.DispatchId,
             a.UseStart,
             a.UseEnd,
             a.Origin,
             a.Destination,
             a.ApplyReason,
             a.ApplicantName,
             a.PassengerCount,
             a.TripType,            // 'single' | 'round'
             a.SingleDistance,      // 可能是字串
             a.RoundTripDistance,   // 可能是字串
             a.Status,
             r.DriverName,
             v.PlateNo
         };

            // 篩選
            if (dateFrom.HasValue)
            {
                var from = dateFrom.Value.Date;
                q = q.Where(x => x.UseStart >= from);
            }
            if (dateTo.HasValue)
            {
                var to = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(x => x.UseStart <= to);
            }
            if (!string.IsNullOrWhiteSpace(driver))
            {
                q = q.Where(x => x.DriverName == driver);
            }
            if (!string.IsNullOrWhiteSpace(applicant))
            {
                q = q.Where(x => x.ApplicantName.Contains(applicant));
            }
            if (!string.IsNullOrWhiteSpace(plate))
            {
                q = q.Where(x => x.PlateNo.Contains(plate));
            }

            // 排序
            q = q.OrderBy(x => x.UseStart);

            // 先抓 rawRows（字串都保留）
            var rawRows = await q.ToListAsync();

            // EF 抓完再轉 DTO
            var rows = rawRows.Select(x => new RecordDto
            {
                Id = x.DispatchId,
                UseStart = x.UseStart,
                UseEnd = x.UseEnd,
                Route = string.Join(" - ", new[] { x.Origin, x.Destination }
                                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                Reason = x.ApplyReason,
                Applicant = x.ApplicantName,
                Seats = x.PassengerCount,
                Km = x.TripType == "single"
                ? ParseDistance(x.SingleDistance):x.TripType == "round"
                ? ParseDistance(x.RoundTripDistance):0,
                Status = x.Status,
                Driver = x.DriverName,
                Plate = x.PlateNo,
                LongShort = x.TripType == "single" ? "短差"
                           : x.TripType == "round" ? "長差"
                           : null
            }).ToList();

            return Ok(rows);
        }
        // 小工具：從字串中抓出數字部分
        private static decimal ParseDistance(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            // 把非數字、小數點的字元移掉，只留下數字
            var cleaned = new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());

            return decimal.TryParse(cleaned, out var value) ? value : 0;
        }



    }
}
