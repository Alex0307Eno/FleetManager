using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
             a.ApplyId,
             a.UseStart,
             a.UseEnd,
             a.Origin,
             a.Destination,
             a.ReasonType,
             a.ApplyReason,
             a.ApplicantName,
             a.PassengerCount,
             a.TripType,            
             a.SingleDistance,      
             a.RoundTripDistance,   
             a.Status,
             r.DriverId,
             r.DriverName,
             v.VehicleId,
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
                ApplyId = x.ApplyId,
                UseStart = x.UseStart,
                UseEnd = x.UseEnd,
                Route = string.Join(" - ", new[] { x.Origin, x.Destination }
                                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                ReasonType = x.ReasonType,
                Reason = x.ApplyReason,
                Applicant = x.ApplicantName,
                Seats = x.PassengerCount,
                Km = x.TripType == "single"
                ? ParseDistance(x.SingleDistance):x.TripType == "round"
                ? ParseDistance(x.RoundTripDistance):0,
                Status = x.Status,
                Driver = x.DriverName,
                DriverId = x.DriverId,
                Plate = x.PlateNo,
                VehicleId = x.VehicleId,
                LongShort = x.TripType == "single" ? "短差"
                           : x.TripType == "round" ? "長差"
                           : null
            }).ToList();

            return Ok(rows);
        }
        // 🔹 查詢單筆
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOne(int id)
        {
            var d = await _db.Dispatches
                .AsNoTracking()
                .Include(x => x.Vehicle)
                .Include(x => x.Driver)
                .Include(x => x.CarApply) // CarApplications
                .FirstOrDefaultAsync(x => x.DispatchId == id);

            if (d == null) return NotFound();

            return Ok(new
            {
                d.DispatchId,
                d.DispatchStatus,
                d.StartTime,
                d.EndTime,
                d.CreatedAt,
                Driver = d.Driver?.DriverName,
                PlateNo = d.Vehicle?.PlateNo,
                Applicant = d.CarApply?.ApplicantName,
                ReasonType = d.CarApply?.ReasonType,
                Reason = d.CarApply?.ApplyReason,
                Origin = d.CarApply?.Origin,
                Destination = d.CarApply?.Destination,
                Seats = d.CarApply?.PassengerCount,
                Status = d.CarApply?.Status
            });
        }


        // 小工具：從字串中抓出數字部分
        private static decimal ParseDistance(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            // 把非數字、小數點的字元移掉，只留下數字
            var cleaned = new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());

            return decimal.TryParse(cleaned, out var value) ? value : 0;
        }
        // 🔹 更新 (Update)
        public class UpdateDispatchDto
        {
            public int? DriverId { get; set; }
            public int? VehicleId { get; set; }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDispatch(int id, [FromBody] UpdateDispatchDto dto)
        {
            var dispatch = await _db.Dispatches.FindAsync(id);
            if (dispatch == null) return NotFound();

            dispatch.DriverId = dto.DriverId;
            dispatch.VehicleId = dto.VehicleId;

            await _db.SaveChangesAsync();
            return Ok(new { message = "更新成功", dispatch.DispatchId, dispatch.DriverId, dispatch.VehicleId });
        }


       




        // 🔹 刪除 (Delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _db.Dispatches.FindAsync(id);
            if (row == null) return NotFound();

            _db.Dispatches.Remove(row);
            await _db.SaveChangesAsync();
            return Ok(new { message = "刪除成功" });
        }


    }
}
