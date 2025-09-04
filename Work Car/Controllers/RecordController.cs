using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Cars.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/dispatch")]
    public class RecordController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public RecordController(ApplicationDbContext db) => _db = db;


        [HttpGet("records")]
        public async Task<ActionResult<IEnumerable<RecordDto>>> GetRecords(
             
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] string? driver,
            [FromQuery] string? applicant,
            [FromQuery] string? plate,
            [FromQuery] string? order)
        {


            var q =
    from d in _db.Dispatches.AsNoTracking()
    join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId into vg
    from v in vg.DefaultIfEmpty()   // 🚗 車輛可空
    join r in _db.Drivers.AsNoTracking() on d.DriverId equals r.DriverId into rg
    from r in rg.DefaultIfEmpty()   // 👨‍✈️ 駕駛可空
    join a in _db.CarApplications.AsNoTracking() on d.ApplyId equals a.ApplyId
    join p in _db.Applicants.AsNoTracking() on a.ApplicantId equals p.ApplicantId
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
          ApplicantName = p.Name,   
          a.PassengerCount,
          a.TripType,
          a.SingleDistance,
          a.RoundTripDistance,
          a.Status,
          DriverId = r != null ? r.DriverId : (int?)null,
          DriverName = r != null ? r.DriverName : null,
          VehicleId = v != null ? v.VehicleId : (int?)null,
          PlateNo = v != null ? v.PlateNo : null
    };

            // 🔒 若為司機角色，只看自己的派工
            if (User.IsInRole("Driver"))
            {
                var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier); // 登入時放的 userId
                if (!int.TryParse(uidStr, out var userId))
                    return Forbid();

                // 假設 Drivers 有 UserId 欄位可對應登入帳號
                var myDriverId = await _db.Drivers
                    .AsNoTracking()
                    .Where(d => d.UserId == userId)
                    .Select(d => d.DriverId)
                    .FirstOrDefaultAsync();

                if (myDriverId == 0)
                    return Forbid(); // 帳號未綁定司機

                q = q.Where(x => x.DriverId == myDriverId);
            }
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
            
            var today = DateTime.Today;

            q = (order ?? "id_desc").ToLower() switch
            {
                // 今天 & 未來先排 → 然後依 UseStart 排
                "today_first" or "future_first"
                    => q.OrderBy(x => x.UseStart >= today ? 0 : 1)
                         .ThenBy(x => x.UseStart),

                // 最新 DispatchId 在最上
                "id_desc" or "latest"
                    => q.OrderByDescending(x => x.DispatchId),

                // UseStart 降冪
                "start_desc"
                    => q.OrderByDescending(x => x.UseStart),

                // 預設：UseStart 升冪
                _ => q.OrderBy(x => x.UseStart)
            };

            // 先抓 rawRows（字串都保留）
            var rawRows = await q.ToListAsync();

            // EF 抓完再轉 DTO
            var rows = rawRows.Select(x =>
            {
                // 先算出公里數
                decimal km = 0;
                if (x.TripType == "single")
                    km = ParseDistance(x.SingleDistance);
                else if (x.TripType == "round")
                    km = ParseDistance(x.RoundTripDistance);

                // 判斷長/短差
                string longShort = km > 30 ? "長差" : "短差";

                return new RecordDto
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
                    Km = km,
                    Status = x.Status,
                    Driver = x.DriverName,
                    DriverId = x.DriverId,
                    Plate = x.PlateNo,
                    VehicleId = x.VehicleId,
                    LongShort = longShort
                };
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
            .Include(x => x.CarApply)
                .ThenInclude(ca => ca.Applicant)   
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
                Applicant = d.CarApply?.Applicant?.Name,   
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
