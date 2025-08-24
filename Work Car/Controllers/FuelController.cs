using Cars.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FuelController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public FuelController(ApplicationDbContext db) { _db = db; }

        // 下拉：車種（用 Vehicles.Model，若你有 Type/Category 就改成那個欄位）
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var cats = await _db.Vehicles
                .Select(v => v.Model ?? v.Brand ?? "未分類")
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            return Ok(cats);
        }

        // 下拉：車牌（可依關鍵字）
        [HttpGet("plates")]
        public async Task<IActionResult> GetPlates([FromQuery] string? q = null, [FromQuery] string? category = null)
        {
            var query = _db.Vehicles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(v => (v.Model ?? v.Brand ?? "") == category);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(v => v.PlateNo.Contains(q));

            var plates = await query
                .OrderBy(v => v.PlateNo)
                .Select(v => new { v.VehicleId, v.PlateNo })
                .Take(50)
                .ToListAsync();

            return Ok(plates);
        }

        // 主查詢：油耗統計
        // 里程 = 期間內同車輛 Max(Odometer) - Min(Odometer)
        // 每公升行駛公里數 = 里程 / (汽油+柴油 公升)
        // 平均公里數 = 里程 / 筆數（簡化定義；你要改成按日/月平均也很容易）
        [HttpGet("stats")]
        public async Task<IActionResult> Stats(
            [FromQuery] string? category = null,
            [FromQuery] string? plate = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var vq = _db.Vehicles.Select(v => new {
                v.VehicleId,
                v.PlateNo,
                Category = v.Model ?? v.Brand ?? "未分類"
            });

            if (!string.IsNullOrWhiteSpace(category))
                vq = vq.Where(v => v.Category == category);

            if (!string.IsNullOrWhiteSpace(plate))
                vq = vq.Where(v => v.PlateNo.Contains(plate));

            var fq = _db.FuelFillUps.AsQueryable();
            if (from.HasValue) fq = fq.Where(f => f.Date >= from.Value);
            if (to.HasValue) fq = fq.Where(f => f.Date < to.Value.AddDays(1));

            // 先把篩到的車輛找出來
            var vehicles = await vq.ToListAsync();
            var ids = vehicles.Select(x => x.VehicleId).ToList();

            // 只取到這批車的油料紀錄
            var fills = await fq.Where(f => ids.Contains(f.VehicleId)).ToListAsync();

            // GroupBy 並彙總
            var result = vehicles
                .GroupJoin(
                    fills,
                    v => v.VehicleId,
                    f => f.VehicleId,
                    (v, fs) =>
                    {
                        var has = fs.Any();
                        var minOdo = has ? fs.Min(x => x.Odometer) : 0;
                        var maxOdo = has ? fs.Max(x => x.Odometer) : 0;
                        var mileage = Math.Max(0, maxOdo - minOdo);

                        decimal gas = fs.Where(x => x.FuelType == "汽油").Sum(x => x.Liters);
                        decimal diesel = fs.Where(x => x.FuelType == "柴油").Sum(x => x.Liters);
                        decimal oil = fs.Where(x => x.FuelType == "機油").Sum(x => x.Liters);

                        var fuelLiters = gas + diesel;
                        decimal? kmPerL = fuelLiters > 0 ? Math.Round((decimal)mileage / fuelLiters, 2) : (decimal?)null;
                        decimal? avgKm = fs.Count() > 0 ? Math.Round((decimal)mileage / fs.Count(), 2) : (decimal?)null;

                        return new
                        {
                            category = v.Category,
                            plate = v.PlateNo,
                            mileageKm = mileage,
                            gasL = Math.Round(gas, 2),
                            dieselL = Math.Round(diesel, 2),
                            oilL = Math.Round(oil, 2),
                            kmPerL,
                            avgKm
                        };
                    })
                .OrderBy(r => r.plate)
                .ToList();

            return Ok(result);
        }
    }
}
