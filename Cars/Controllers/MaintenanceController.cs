using Cars.Data;
using Cars.Models;
using Cars.Services;
using Cars.Features.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.ApiControllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class MaintenanceController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public MaintenanceController(ApplicationDbContext db) { _db = db; }

        #region 車輛基本狀態
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
        // 取得車輛詳細資料
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

        // 更新車輛狀態
        [HttpPut("vehicle/{vehicleId}/status")]
        public async Task<IActionResult> UpdateVehicleStatus(int vehicleId, [FromBody] string status)
        {
            var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.VehicleId == vehicleId);
            if (v == null) return NotFound();
            v.Status = status?.Trim();
            var (ok,err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return NoContent();
        }
        #endregion

        #region 保養紀錄 CRUD
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
                    m.Note,
                    m.NextDueDate
                });

            return Ok(await q.ToListAsync());
        }

        // 新增保養紀錄
        

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] MaintainanceCreateDto dto)
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
                Note = dto.Note,
                NextDueDate = dto.NextDueDate
            };
            _db.VehicleMaintenances.Add(entity);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "新增成功", id = entity.VehicleMaintenanceId });
        }
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateMaintenance(int id, [FromBody] VehicleMaintenance body)
        {
            var m = await _db.VehicleMaintenances.FirstOrDefaultAsync(x => x.VehicleMaintenanceId == id);
            if (m == null) return NotFound();

            // 只更新允許的欄位
            m.Date = body.Date == default ? m.Date : body.Date;
            m.Odometer = body.Odometer;
            m.Item = body.Item ?? m.Item;
            m.Unit = body.Unit;
            m.Qty = body.Qty;
            m.Amount = body.Amount;
            m.Vendor = body.Vendor;
            m.Note = body.Note;
            m.NextDueDate = body.NextDueDate;

            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return NoContent();
        }
        // 刪除
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMaintenance(int id)
        {
            var one = await _db.VehicleMaintenances.FindAsync(id);
            if (one == null) return NotFound();
            _db.VehicleMaintenances.Remove(one);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "已刪除" });
        }
        
        
        #endregion

        #region 維修紀錄 CRUD
        // 新增報修
        [HttpPost("repair")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateRepair([FromBody] RepairRequestDto dto)
        {
            if (dto.VehicleId <= 0) return BadRequest("VehicleId 必填");
            if (string.IsNullOrWhiteSpace(dto.Issue)) return BadRequest("故障內容必填");

            var entity = new VehicleRepair
            {
                VehicleId = dto.VehicleId,
                PlateNo = dto.PlateNo ?? "",
                Date = dto.Date,
                Place = dto.Place,
                Issue = dto.Issue.Trim(),
                CostEstimate = dto.CostEstimate,
                Vendor = dto.Vendor,
                Note =  dto.Note,
                Status = "待處理"
            };

            _db.VehicleRepairs.Add(entity);

            //  同時把車輛狀態改為「維修中」
            var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.VehicleId == dto.VehicleId);
            if (v != null)
                v.Status = "維修中";

            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "報修已送出", id = entity.RepairRequestId });
        }
        //更新維修
        [HttpPut("repair/{id:int}")]
        public async Task<IActionResult> UpdateRepair(int id, [FromBody] RepairRequestDto dto)
        {
            var repair = await _db.VehicleRepairs.FindAsync(id);
            if (repair == null) return NotFound();

            repair.Date = dto.Date;
            repair.Place = dto.Place;
            repair.Issue = dto.Issue ?? repair.Issue;
            repair.CostEstimate = dto.CostEstimate;
            repair.Vendor = dto.Vendor;
            repair.Note = dto.Note;
            // 保持原有的 Status，不要強制改

            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "updated" });
        }


        // 查詢某車的報修紀錄
        [HttpGet("repairs")]
        public async Task<IActionResult> ListRepairs([FromQuery] int vehicleId)
        {
            var q = _db.VehicleRepairs
                .Where(r => r.VehicleId == vehicleId)
                .OrderByDescending(r => r.Date)
                .Select(r => new {
                    repairRequestId = r.RepairRequestId,
                    date = r.Date != null ? r.Date.ToString("yyyy-MM-dd") : "",
                    place = r.Place ?? "",
                    issue = r.Issue ?? "",
                    status = r.Status ?? "",
                    costEstimate = r.CostEstimate ?? 0,
                    vendor = r.Vendor ?? "",
                    note = r.Note ?? ""
                });

            return Ok(await q.ToListAsync());
        }
        // 刪除報修紀錄
        [HttpDelete("repair/{id}")]
        public async Task<IActionResult> DeleteRepair(int id)
        {
            var record = await _db.VehicleRepairs.FindAsync(id);
            if (record == null) return NotFound();

            _db.VehicleRepairs.Remove(record);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "報修紀錄已刪除", id });
        }
        //維修完成更改狀態
        [HttpPut("repair/{id:int}/status")]
        public async Task<IActionResult> UpdateRepairStatus(int id, [FromBody] string status)
        {
            var repair = await _db.VehicleRepairs.FindAsync(id);
            if (repair == null) return NotFound();

            repair.Status = status;
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "status updated" });
        }
        #endregion

        #region 驗車紀錄 CRUD
        [HttpGet("inspections")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetInspections([FromQuery] int vehicleId)
        {
            if (vehicleId <= 0) return BadRequest(new { message = "vehicleId 必填" });

            var list = await _db.VehicleInspections
                .AsNoTracking()
                .Where(x => x.VehicleId == vehicleId)
                .OrderByDescending(x => x.InspectionDate)
                .Select(x => new {
                    x.InspectionId,
                    x.VehicleId,
                    date = x.InspectionDate,
                    station = x.Station,
                    result = x.Result,
                    nextDueDate = x.NextDueDate,
                    odometerKm = x.OdometerKm,
                    notes = x.Notes
                })
                .ToListAsync();

            return Ok(list);
        }

        

        [HttpPost("inspections")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateInspection([FromBody] InspectionDto dto)
        {
            if (dto == null || dto.VehicleId <= 0) return BadRequest(new { message = "資料不完整" });

            var m = new VehicleInspection
            {
                VehicleId = dto.VehicleId,
                InspectionDate = dto.InspectionDate,
                Station = dto.Station,
                Result = string.IsNullOrWhiteSpace(dto.Result) ? "合格" : dto.Result,
                NextDueDate = dto.NextDueDate,
                OdometerKm = dto.OdometerKm,
                Notes = dto.Notes
            };

            _db.VehicleInspections.Add(m);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { id = m.InspectionId });
        }

        [HttpPut("inspections/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateInspection(int id, [FromBody] InspectionDto dto)
        {
            var m = await _db.VehicleInspections.FindAsync(id);
            if (m == null) return NotFound();

            m.VehicleId = dto.VehicleId;
            m.InspectionDate = dto.InspectionDate;
            m.Station = dto.Station;
            m.Result = string.IsNullOrWhiteSpace(dto.Result) ? m.Result : dto.Result;
            m.NextDueDate = dto.NextDueDate;
            m.OdometerKm = dto.OdometerKm;
            m.Notes = dto.Notes;

            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "updated" });
        }

        [HttpDelete("inspections/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteInspection(int id)
        {
            var m = await _db.VehicleInspections.FindAsync(id);
            if (m == null) return NotFound();
            _db.VehicleInspections.Remove(m);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "deleted" });
        }
        #endregion
        
        #region 違規紀錄 CRUD
        // --------------------- 違規紀錄 CRUD ---------------------
        [HttpGet("violations")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetViolations([FromQuery] int vehicleId)
        {
            if (vehicleId <= 0) return BadRequest(new { message = "vehicleId 必填" });

            var list = await _db.VehicleViolations
                .AsNoTracking()
                .Where(x => x.VehicleId == vehicleId)
                .OrderByDescending(x => x.ViolationDate)
                .Select(x => new {
                    x.ViolationId,
                    x.VehicleId,
                    date = x.ViolationDate,
                    location = x.Location,
                    category = x.Category,
                    fineAmount = x.FineAmount,
                    status = x.Status,
                    dueDate = x.DueDate,
                    paidDate = x.PaidDate,
                    notes = x.Notes
                })
                .ToListAsync();

            return Ok(list);
        }

        

        [HttpPost("violations")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateViolation([FromBody] ViolationDto dto)
        {
            if (dto == null || dto.VehicleId <= 0) return BadRequest(new { message = "資料不完整" });

            var m = new VehicleViolation
            {
                VehicleId = dto.VehicleId,
                ViolationDate = dto.ViolationDate,
                Location = dto.Location,
                Category = dto.Category,
                FineAmount = dto.FineAmount,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "未繳" : dto.Status,
                DueDate = dto.DueDate,
                PaidDate = dto.PaidDate,
                Notes = dto.Notes
            };

            _db.VehicleViolations.Add(m);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { id = m.ViolationId });
        }

        [HttpPut("violations/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateViolation(int id, [FromBody] ViolationDto dto)
        {
            var m = await _db.VehicleViolations.FindAsync(id);
            if (m == null) return NotFound();

            m.VehicleId = dto.VehicleId;
            m.ViolationDate = dto.ViolationDate;
            m.Location = dto.Location;
            m.Category = dto.Category;
            m.FineAmount = dto.FineAmount;
            m.Status = string.IsNullOrWhiteSpace(dto.Status) ? m.Status : dto.Status;
            m.DueDate = dto.DueDate;
            m.PaidDate = dto.PaidDate;
            m.Notes = dto.Notes;

            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "updated" });
        }

        [HttpDelete("violations/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteViolation(int id)
        {
            var m = await _db.VehicleViolations.FindAsync(id);
            if (m == null) return NotFound();
            _db.VehicleViolations.Remove(m);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            return Ok(new { message = "deleted" });
        }
        #endregion

    }
}
