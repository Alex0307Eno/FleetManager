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
                .Select(v => new { vehicleId = v.VehicleId, plate = v.PlateNo, status = v.Status })
                .ToListAsync();

            return Ok(list);
        }

        [HttpGet("vehicle/{id}")]
        public async Task<IActionResult> GetVehicleDetail(int id)
        {
            var v = await _db.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.VehicleId == id);

            if (v == null) return NotFound();

            return Ok(new
            {
                v.VehicleId,
                plate = v.PlateNo,
                v.Status,
                v.Source,
                v.ApprovalNo,
                v.PurchaseDate,
                v.Value,
                v.LicenseDate,
                v.StartUseDate,
                v.InspectionDate,
                v.EngineCC,
                v.EngineNo,
                v.Brand,
                v.Year,
                v.Model,
                v.Type,
                v.Retired
            });
        }


        // 查某車的保養紀錄
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int vehicleId)
        {
            var q = _db.VehicleMaintenances
                .Where(m => m.VehicleId == vehicleId)
                .OrderByDescending(m => m.Date)
                .Select(m => new {
                    id = m.VehicleMaintenanceId,
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
            return Ok(new { message = "新增成功", id = entity.VehicleMaintenanceId });
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
        // === 報修申請 DTO ===
        public sealed class RepairRequestDto
        {
            public int VehicleId { get; set; }
            public string? PlateNo { get; set; }
            public DateTime Date { get; set; }
            public string Issue { get; set; } = "";
            public decimal? CostEstimate { get; set; }
            public string? Note { get; set; }
        }

        // 新增報修
        [HttpPost("repair")]
        public async Task<IActionResult> CreateRepair([FromBody] RepairRequestDto dto)
        {
            if (dto.VehicleId <= 0) return BadRequest("VehicleId 必填");
            if (string.IsNullOrWhiteSpace(dto.Issue)) return BadRequest("故障內容必填");

            var entity = new RepairRequest
            {
                VehicleId = dto.VehicleId,
                PlateNo = dto.PlateNo ?? "",
                Date = dto.Date,
                Issue = dto.Issue,
                CostEstimate = dto.CostEstimate,
                Note = dto.Note,
                Status = "待處理"
            };

            _db.RepairRequests.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new { message = "報修已送出", id = entity.RepairRequestId });
        }

        // 查詢某車的報修紀錄
        [HttpGet("repairs")]
        public async Task<IActionResult> ListRepairs([FromQuery] int vehicleId)
        {
            var q = _db.RepairRequests
                .Where(r => r.VehicleId == vehicleId)
                .OrderByDescending(r => r.Date)
                .Select(r => new {
                    repairRequestId = r.RepairRequestId,
                    date = r.Date != null ? r.Date.ToString("yyyy-MM-dd") : "",
                    issue = r.Issue ?? "",
                    status = r.Status ?? "",
                    costEstimate = r.CostEstimate ?? 0,
                    note = r.Note ?? ""
                });

            return Ok(await q.ToListAsync());
        }


        // 更新狀態（例如 維修完成）
        [HttpPut("vehicle/{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
        {
            var v = await _db.Vehicles.FindAsync(id);
            if (v == null) return NotFound();

            v.Status = status;
            await _db.SaveChangesAsync();

            return Ok(new { message = "狀態已更新", status });
        }



    }
}
