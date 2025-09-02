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
        [Authorize(Roles = "Admin,Applicant")]
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
                // 優先用 Applicants.UserId 關聯（建議的做法）
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
                        // 後備方案：用名字比對（若你的 View 有 ApplicantName）
                        query = query.Where(o => o.ApplicantName == userName);
                    }
                    else
                    {
                        return Ok(Array.Empty<object>());
                    }
                }
                else if (!string.IsNullOrEmpty(userName))
                {
                    // 身分不是 int 的情形，退回用名稱
                    query = query.Where(o => o.ApplicantName == userName);
                }
                else
                {
                    return Ok(Array.Empty<object>());
                }
            }
            var data = await query
                .Select(o => new
                {
                    o.VehicleId,
                    o.PlateNo,
                    o.DriverId,
                    o.DriverName,
                    o.ApplyId,
                    ApplicantId = o.ApplicantId,
                    ApplicantName = o.ApplicantName, // 直接用 View.Name
                    ApplicantDept = o.ApplicantDept, // 直接用 View.Dept
                    o.PassengerCount,
                    o.UseDate,
                    o.UseTime,
                    o.Route,
                    o.Reason,
                    o.TripDistance,
                    o.TripType,
                    o.Status
                })
                .ToListAsync();

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
