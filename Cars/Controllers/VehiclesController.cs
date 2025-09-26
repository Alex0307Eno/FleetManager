using Cars.Data;
using Cars.Features.Vehicles;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.ApiControllers
{
    [Authorize]
    [Route("Vehicles")]
    [Authorize(Roles = "Admin")]
    public class VehiclesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly VehicleService _vehicleService;

        public VehiclesController(ApplicationDbContext db, VehicleService vehicleService) 
        {
            _db = db;
            _vehicleService = vehicleService;
        } 

       
        #region 行車歷程清單
        // API：行車歷程清單（查詢 + 回傳 JSON）
        [HttpGet("TripStats")]
        public async Task<IActionResult> TripStats(
            DateTime? dateFrom, DateTime? dateTo,
            int? driverId, string? plate, string? applicant, string? dept, string? longShort)
        {
            var q = _db.CarApplications
         .Include(x => x.DispatchOrders)
         .ThenInclude(d => d.Driver)
         .Include(x => x.DispatchOrders)
         .ThenInclude(d => d.Vehicle)
         .Include(x => x.Applicant)   
         .AsNoTracking()
         .Select(x => new
    {
        id = x.ApplyId,
        driveDate = x.UseStart.Date,
        driverId = x.DispatchOrders
            .Select(d => d.Driver.DriverId)
            .FirstOrDefault(),
        driverName = x.DispatchOrders
            .Select(d => d.Driver.DriverName)
            .FirstOrDefault(),
        plateNo = x.DispatchOrders
            .Select(d => d.Vehicle.PlateNo)
            .FirstOrDefault(),
        applicantDept = x.Applicant != null ? x.Applicant.Dept : null,  
        applicantName = x.Applicant != null ? x.Applicant.Name : null,   
        km = x.TripType == "single"
            ? (x.SingleDistance ?? 0)
            : (x.RoundTripDistance ?? 0),
        trips = 1,
        longShort = x.TripType == "single" ? "短差" :
                    x.TripType == "round" ? "長差" : "未知"
    });


            // 篩選條件
            if (dateFrom.HasValue) q = q.Where(x => x.driveDate >= dateFrom.Value.Date);
            if (dateTo.HasValue) q = q.Where(x => x.driveDate <= dateTo.Value.Date);
            if (driverId.HasValue) q = q.Where(x => x.driverId == driverId.Value);
            if (!string.IsNullOrWhiteSpace(plate)) q = q.Where(x => x.plateNo != null && x.plateNo.Contains(plate));
            if (!string.IsNullOrWhiteSpace(applicant)) q = q.Where(x => x.applicantName != null && x.applicantName.Contains(applicant));
            if (!string.IsNullOrWhiteSpace(dept)) q = q.Where(x => x.applicantDept != null && x.applicantDept.Contains(dept));
            if (!string.IsNullOrWhiteSpace(longShort))
                q = q.Where(x => x.longShort == longShort.Trim());
            var list = await q
                .OrderBy(x => x.driveDate)
                .ThenBy(x => x.driverName)
                .ThenBy(x => x.plateNo)
                .ToListAsync();

            // 統一格式
            var result = list.Select(x => new
            {
                x.id,
                x.driveDate,
                x.driverName,
                x.plateNo,
                x.applicantDept,
                x.applicantName,
                km = Math.Round(x.km, 0),
                x.trips,
                x.longShort
            });

            return Json(result);
        }
        #endregion

        #region 行車歷程統計圖表
        [HttpGet("ChartData")]
        public async Task<IActionResult> ChartData(DateTime? dateFrom, DateTime? dateTo, string type = "long")
        {
            // 1) 先把原始單據抓出來（含派工 -> 取 DriverName / PlateNo）
            var q = _db.CarApplications
                .Include(x => x.DispatchOrders).ThenInclude(d => d.Driver)
                .Include(x => x.DispatchOrders).ThenInclude(d => d.Vehicle)
                .AsNoTracking();

            if (dateFrom.HasValue) q = q.Where(x => x.UseStart.Date >= dateFrom.Value.Date);
            if (dateTo.HasValue) q = q.Where(x => x.UseStart.Date <= dateTo.Value.Date);

            // 長差/短差：你的資料欄 TripType = "round" 表長差；"single" 表短差
            if (string.Equals(type, "long", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.TripType == "round");
            else if (string.Equals(type, "short", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.TripType == "single");

            // 2) 先投影為可計算的基本欄位（里程用字串，等會在記憶體解析）
            var raw = await q.Select(x => new
            {
                Driver = x.DispatchOrders.Select(d => d.Driver.DriverName).FirstOrDefault(),
                Plate = x.DispatchOrders.Select(d => d.Vehicle.PlateNo).FirstOrDefault(),
                Single = x.SingleDistance,
                Round = x.RoundTripDistance
            }).ToListAsync();

            
            // 3) 解析里程
            decimal ParseKm(decimal? single, decimal? round)
            {
                if (round.HasValue) return round.Value;
                if (single.HasValue) return single.Value;
                return 0m;
            }

            // 4) 彙整：駕駛
            var driverAgg = raw
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Driver) ? "—" : x.Driver)
                .Select(g => new
                {
                    name = g.Key,
                    km = g.Sum(v => ParseKm(v.Single, v.Round)), 
                    trips = g.Count()
                })
                .OrderByDescending(x => x.km)
                .Take(8)
                .ToList();


            // 5) 彙整：車牌
            var plateAgg = raw
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Plate) ? "—" : x.Plate)
                .Select(g => new
                {
                    name = g.Key,
                    km = g.Sum(v => ParseKm(v.Single, v.Round)),
                    trips = g.Count()
                })
                .OrderByDescending(x => x.km)
                .Take(8)
                .ToList();

            return Json(new { drivers = driverAgg, vehicles = plateAgg });
        }
        #endregion

        #region 查詢可用車輛
        // 查詢可用車輛（排除 使用中 / 維修中 / 該時段已被派工）
        [HttpGet("/api/vehicles-available")]
        public async Task<IActionResult> GetAvailableVehicles(
         [FromQuery] DateTime from,
         [FromQuery] DateTime to,
         [FromQuery] int? capacity = null)
        {
            if (from == default || to == default)
                return BadRequest(ApiResponse.Fail<object>("請提供有效的時間區間"));

            if (to <= from)
                return BadRequest(ApiResponse.Fail<object>("結束時間必須晚於開始時間"));

            var list = await _vehicleService.GetAvailableVehiclesAsync(from, to, capacity);

            return Ok(ApiResponse.Ok(list, "可用車輛清單取得成功"));
            #endregion

        }
        //查詢車輛座位數上限
        [HttpGet("/api/max-capacity")]
            public async Task<IActionResult> GetMaxCapacity([FromQuery] DateTime from, [FromQuery] DateTime to)
            {
                if (from == default || to == default || from >= to)
                    return BadRequest(new { message = "時間參數錯誤" });

                var max = await _vehicleService.GetMaxAvailableCapacityAsync(from, to);
                return Ok(new { max });
            }
        
    }
}
