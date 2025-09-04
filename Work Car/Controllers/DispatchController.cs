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
            }

            // 先把原始資料取回（含申請單的 Trip/距離）
            var raw = await (
                from o in query
                join v0 in _db.Vehicles on o.VehicleId equals v0.VehicleId into vv
                from v in vv.DefaultIfEmpty()
                join d0 in _db.Drivers on o.DriverId equals d0.DriverId into dd
                from dr in dd.DefaultIfEmpty()
                join a0 in _db.CarApplications on o.ApplyId equals a0.ApplyId into aa
                from a in aa.DefaultIfEmpty()
                select new
                {
                    o.VehicleId,
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

                    // 兩邊的 Trip/距離都帶回來，稍後在記憶體端判斷
                    O_TripType = o.TripType,
                    O_TripDistance = o.TripDistance,
                    A_TripType = a != null ? a.TripType : null,
                    A_SingleDistance = a != null ? a.SingleDistance : null,
                    A_RoundTripDistance = a != null ? a.RoundTripDistance : null,

                    o.Status
                }
            ).ToListAsync();  // ← 先落地到記憶體

            // 在記憶體端依 TripType 組合「最終要顯示的距離」
            var data = raw.Select(x =>
            {
                // 1) 先決定有效的 TripType（優先用申請單）
                var t = string.IsNullOrWhiteSpace(x.A_TripType) ? x.O_TripType : x.A_TripType;
                var tNorm = (t ?? "").Trim().ToLowerInvariant();
                bool isSingle =
                    tNorm == "單程" || tNorm == "single" || tNorm == "oneway" || tNorm == "one-way";

                // 2) 依 TripType 選距離；若申請單沒填，退回舊欄位 o.TripDistance
                var kmRaw = isSingle
                    ? (string.IsNullOrWhiteSpace(x.A_SingleDistance) ? x.O_TripDistance : x.A_SingleDistance)
                    : (string.IsNullOrWhiteSpace(x.A_RoundTripDistance) ? x.O_TripDistance : x.A_RoundTripDistance);

                var kmText = string.IsNullOrWhiteSpace(kmRaw) ? "" :
                             (kmRaw.Contains("公里") ? kmRaw : kmRaw + " 公里");

                return new
                {
                    x.VehicleId,
                    x.PlateNo,
                    x.Plate,
                    x.DriverId,
                    x.DriverName,
                    x.ApplyId,
                    x.ApplicantId,
                    x.ApplicantName,
                    x.ApplicantDept,
                    x.PassengerCount,
                    x.UseDate,
                    x.UseTime,
                    x.Route,
                    x.Reason,

                    tripDistance = kmText,   // ← 前端用這個顯示
                    tripType = t,            // ← 若要顯示原始 TripType 也給你
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
