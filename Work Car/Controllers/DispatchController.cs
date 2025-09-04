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

            // 基本查詢來源
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
                        query = query.Where(o => o.ApplicantId == myApplicantId.Value);
                    else if (!string.IsNullOrEmpty(userName))
                        query = query.Where(o => o.ApplicantName == userName);
                    else
                        return Ok(Array.Empty<object>());
                }
                else if (!string.IsNullOrEmpty(userName))
                {
                    query = query.Where(o => o.ApplicantName == userName);
                }
                else
                {
                    return Ok(Array.Empty<object>());
                }
            }else if (User.IsInRole("Manager"))
{
    // 取目前使用者的部門/科室（從 Applicants.UserId 對應）
    if (int.TryParse(uidStr, out var userId))
    {
        var myDept = await _db.Applicants
            .Where(a => a.UserId == userId)
            .Select(a => a.Dept)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(myDept))
        {
            // ★ Manager 看到同部門/科室所有申請
            query = query.Where(o => o.ApplicantDept == myDept);
        }
        else if (!string.IsNullOrEmpty(userName))
        {
            // 找不到部門就退回只能看自己
            query = query.Where(o => o.ApplicantName == userName);
        }
        else
        {
            return Ok(Array.Empty<object>());
        }
    }
    else if (!string.IsNullOrEmpty(userName))
    {
        // 沒有 userId 但有帳號名稱，保守退回只看自己
        query = query.Where(o => o.ApplicantName == userName);
    }
    else
    {
        return Ok(Array.Empty<object>());
    }
}

            // 把 TripType/距離從 CarApplications 帶出來
            var raw = await (
                from o in query
                join a0 in _db.CarApplications on o.ApplyId equals a0.ApplyId into aa
                from a in aa.DefaultIfEmpty()
                join d0 in _db.Drivers on o.DriverId equals d0.DriverId into dd
                from dr in dd.DefaultIfEmpty()
                join v0 in _db.Vehicles on o.VehicleId equals v0.VehicleId into vv
                from v in vv.DefaultIfEmpty()
                select new
                {
                    // 顯示用欄位
                    o.ApplyId,
                    o.VehicleId,
                    PlateNo = (o.PlateNo ?? v.PlateNo) ?? "未指派",
                    Plate = (o.PlateNo ?? v.PlateNo) ?? "未指派",

                    o.DriverId,
                    DriverName = (o.DriverName ?? dr.DriverName) ?? "未指派",

                    o.ApplicantId,
                    o.ApplicantName,
                    o.ApplicantDept,
                    o.PassengerCount,
                    o.UseDate,
                    o.UseTime,
                    o.Route,
                    o.Reason,

                    // ★ 只用 CarApply 的欄位來決定里程
                    A_TripType = a != null ? a.TripType : null,
                    A_SingleDistance = a != null ? a.SingleDistance : null,
                    A_RoundTripDistance = a != null ? a.RoundTripDistance : null,

                    o.Status
                }
            ).ToListAsync();

           

            var data = raw.Select(x =>
            {
                // 嚴格只看 single / round
                var t = (x.A_TripType ?? "").Trim().ToLowerInvariant();

                // 不再做任何 fallback，只依 TripType 選距離
                var tripDistance =
                    (t == "single")
                        ? (x.A_SingleDistance ?? "")
                        : (t == "round")
                            ? (x.A_RoundTripDistance ?? "")
                            : "";

                return new
                {
                    x.ApplyId,
                    x.VehicleId,
                    x.PlateNo,
                    x.Plate,

                    x.DriverId,
                    x.DriverName,

                    x.ApplicantId,
                    x.ApplicantName,
                    x.ApplicantDept,

                    x.PassengerCount,
                    x.UseDate,
                    x.UseTime,
                    x.Route,
                    x.Reason,

                    // ★ 用車頁面就綁這個（注意：這裡改成「大寫 T」）
                    TripDistance = tripDistance,

                    // （臨時除錯用，前端可在 console 看看）
                    TripTypeRaw = x.A_TripType,

                    x.Status
                };
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
