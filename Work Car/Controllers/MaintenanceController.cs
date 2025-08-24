using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaintenanceController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public MaintenanceController(ApplicationDbContext db) { _db = db; }

        // 取得車牌清單（下拉用）
        [HttpGet("vehicles")]
        public async Task<IActionResult> GetVehicles()
        {
            var list = await _db.Vehicles
                .OrderBy(v => v.PlateNo)
                .Select(v => new { vehicleId = v.VehicleId, plate = v.PlateNo })
                .ToListAsync();

            return Ok(list);
        }

        // 查某車的保養紀錄
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int vehicleId)
        {
            var q = _db.VehicleMaintenances
                .Where(m => m.VehicleId == vehicleId)
                .OrderByDescending(m => m.Date)
                .Select(m => new {
                    m.Id,
                    date = m.Date.ToString("yyyy-MM-dd"),
                    m.Odometer,
                    m.Item,
                    m.Unit,
                    m.Qty,
                    m.Amount,
                    subtotal = (m.Qty ?? 0) * (m.Amount ?? 0),
                    m.Vendor,
                    m.Note
                });

            return Ok(await q.ToListAsync());
        }

        // 新增保養紀錄
        public sealed class CreateDto
        {
            public int VehicleId { get; set; }
            public string? VehiclePlate { get; set; }
            public DateTime Date { get; set; }
            public int? Odometer { get; set; }
            public string Item { get; set; } = "";
            public string? Unit { get; set; }
            public decimal? Qty { get; set; }
            public decimal? Amount { get; set; }
            public string? Vendor { get; set; }
            public string? Note { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDto dto)
        {
            if (dto.VehicleId <= 0) return BadRequest("VehicleId 必填");
            if (string.IsNullOrWhiteSpace(dto.Item)) return BadRequest("保養項目必填");

            var entity = new VehicleMaintenance
            {
                VehicleId = dto.VehicleId,
                VehiclePlate = dto.VehiclePlate,
                Date = dto.Date,
                Odometer = dto.Odometer,
                Item = dto.Item.Trim(),
                Unit = dto.Unit,
                Qty = dto.Qty,
                Amount = dto.Amount,
                Vendor = dto.Vendor,
                Note = dto.Note
            };
            _db.VehicleMaintenances.Add(entity);
            await _db.SaveChangesAsync();
            return Ok(new { message = "新增成功", id = entity.Id });
        }

        // 刪除
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var one = await _db.VehicleMaintenances.FindAsync(id);
            if (one == null) return NotFound();
            _db.VehicleMaintenances.Remove(one);
            await _db.SaveChangesAsync();
            return Ok(new { message = "已刪除" });
        }
    }
}
