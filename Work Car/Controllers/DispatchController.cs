using Cars.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Cars.Models;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DispatchController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public DispatchController(ApplicationDbContext db)
        {
            _db = db;
        }

        // 取得派車單列表
        [HttpGet("list")]
        public IActionResult GetList()
        {
            var data = (from d in _db.Dispatches
                        join v in _db.Vehicles on d.VehicleId equals v.VehicleId into dv
                        from v in dv.DefaultIfEmpty()
                        join r in _db.Drivers on d.DriverId equals r.DriverId into dr
                        from r in dr.DefaultIfEmpty()
                        join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                        select new
                        {
                            id = d.DispatchId,
                            date = a.UseStart,
                            start = a.UseStart.ToString("HH:mm"),
                            end = a.UseEnd.ToString("HH:mm"),
                            destination = a.Origin + "-" + a.Destination,
                            reason = a.ApplyReason,
                            applicant = a.ApplicantName,
                            people = a.Seats,
                            km = a.TripType == "單程" ? a.SingleDistance : a.RoundTripDistance,
                            status = a.Status,
                            driver = r != null ? r.DriverName : null,
                            car = v != null ? v.PlateNo : null
                        }).ToList();

            return Ok(data);
        }

        // 指派駕駛與車輛
        [HttpPost("assign")]
        public IActionResult Assign([FromBody] AssignDto dto)
        {
            var dispatch = _db.Dispatches.FirstOrDefault(x => x.DispatchId == dto.DispatchId);
            if (dispatch == null) return NotFound(new { message = "找不到派車單" });

            dispatch.DriverId = dto.DriverId;
            dispatch.VehicleId = dto.VehicleId;

            _db.SaveChanges();
            return Ok(new { message = "指派成功" });
        }
    }

    public class AssignDto
    {
        public int DispatchId { get; set; }
        public int? DriverId { get; set; }
        public int? VehicleId { get; set; }
    }
}
