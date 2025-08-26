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
            var data = _db.DispatchOrders
                .Select(o => new
                {
                    vehicleId = o.VehicleId,
                    plateNo = o.PlateNo,
                    driverId = o.DriverId,
                    driverName = o.DriverName,
                    applyId = o.ApplyId,
                    applicantName = o.ApplicantName,
                    applicantDept = o.ApplicantDept,
                    passengerCount = o.PassengerCount,
                    useDate = o.UseDate,
                    useTime = o.UseTime,
                    route = o.Route,
                    reason = o.Reason,
                    tripDistance = o.TripDistance,
                    tripType = o.TripType,
                    status = o.Status
                })
                .ToList();

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

        [HttpGet("/api/vehicles/list")]
        public IActionResult GetVehicles()
        {
            var vehicles = _db.Vehicles
                .Select(v => new
                {
                    vehicleId = v.VehicleId,
                    plateNo = v.PlateNo,
                    // 判斷是否已被派車 (車輛已經被某張申請單指派)
                    isAssigned = _db.Dispatches.Any(d => d.VehicleId == v.VehicleId
                                                       && d.CarApply.Status == "審核中"),
                    inUse = _db.Dispatches.Any(d => d.VehicleId == v.VehicleId
                                                  && d.CarApply.Status == "進行中")
                })
                .ToList();

            return Ok(vehicles);
        }

    }

    public class AssignDto
    {
        public int DispatchId { get; set; }
        public int? DriverId { get; set; }
        public int? VehicleId { get; set; }
    }
}
