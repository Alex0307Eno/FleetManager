using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace Cars.Controllers
{
    [Authorize]
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
        [Authorize(Roles = "Admin,Applicant,Manager")]
        [HttpGet("list")]
        public async Task<IActionResult> GetList()
        {
            // 取得目前登入者
            var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            // 先取出基本查詢
            var query = _db.DispatchOrders.AsQueryable();

            // Admin 可看全部；非 Admin 只能看自己
            if (!User.IsInRole("Admin"))
            {
                if (int.TryParse(uidStr, out var userId))
                {
                    var myApplicantId = await _db.Applicants
                        .Where(a => a.UserId == userId)
                        .Select(a => (int?)a.ApplicantId)
                        .FirstOrDefaultAsync();

                    if (myApplicantId.HasValue)
                    {
                        query = query.Where(o => o.ApplicantId == myApplicantId.Value);
                    }
                    else if (!string.IsNullOrEmpty(userName))
                    {
                        query = query.Where(o => o.ApplicantName == userName);
                    }
                    else
                    {
                        return Ok(Array.Empty<object>());
                    }
                }
                else if (!string.IsNullOrEmpty(userName))
                {
                    query = query.Where(o => o.ApplicantName == userName);
                }
                else
                {
                    return Ok(Array.Empty<object>());
                }
            }

            // ✅ 補齊車牌與駕駛姓名：o.PlateNo / o.DriverName 若為 null，就用關聯表補
            var data = await (
                from o in query
                join v0 in _db.Vehicles on o.VehicleId equals v0.VehicleId into vv
                from v in vv.DefaultIfEmpty()
                join d0 in _db.Drivers on o.DriverId equals d0.DriverId into dd
                from dr in dd.DefaultIfEmpty()
                select new
                {
                    o.VehicleId,
                    // 前端可用任一；兩個欄位都給，避免大小寫或命名不一致
                    PlateNo = (o.PlateNo ?? v.PlateNo) ?? "未指派",
                    Plate = (o.PlateNo ?? v.PlateNo) ?? "未指派",

                    o.DriverId,
                    DriverName = (o.DriverName ?? dr.DriverName) ?? "未指派",

                    o.ApplyId,
                    ApplicantId = o.ApplicantId,
                    ApplicantName = o.ApplicantName,
                    ApplicantDept = o.ApplicantDept,

                    o.PassengerCount,
                    o.UseDate,
                    o.UseTime,
                    o.Route,
                    o.Reason,
                    o.TripDistance,
                    o.TripType,
                    o.Status
                }
            ).ToListAsync();

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
