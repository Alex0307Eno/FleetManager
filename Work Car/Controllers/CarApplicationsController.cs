using Microsoft.AspNetCore.Mvc;
using Cars.Data;
using Cars.Models;
using Cars.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarApplicationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CarApplicationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 建立申請單（含搭乘人員清單）
        public class CarApplyDto
        {
            public CarApply Application { get; set; }
            public List<CarPassenger> Passengers { get; set; } = new();
        }

       

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CarApplyDto dto, [FromServices] AutoDispatcher dispatcher)
        {
            if (dto == null || dto.Application == null)
                return BadRequest("申請資料不得為空");

            var model = dto.Application;

            if (model.UseStart == default || model.UseEnd == default)
                return BadRequest("起訖時間不得為空");
            if (model.UseEnd <= model.UseStart)
                return BadRequest("結束時間必須晚於起始時間");


            // 先存申請單，讓 DB 自動產生 ApplyId
            _context.CarApplications.Add(model);
            await _context.SaveChangesAsync();

            if (model.PurposeType == "公務車(可選車)")
            {
                if (model.VehicleId == null) return BadRequest("請選擇車輛");

                var vehicle = await _context.Vehicles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.VehicleId == model.VehicleId.Value);
                if (vehicle == null) return BadRequest("車輛不存在");

                if ((vehicle.Status ?? "").Trim() != "可用")
                    return BadRequest("該車輛目前不可用，請重新選擇");

                var vUsed = await _context.Dispatches.AnyAsync(d =>
                    d.VehicleId == model.VehicleId &&
                    model.UseStart < d.EndTime &&
                    d.StartTime < model.UseEnd);
                if (vUsed) return BadRequest("該車於申請時段已被派用，請改選其他車或調整時間");

                // 找當班駕駛
                model.DriverId = await dispatcher.FindOnDutyDriverIdAsync(model.UseStart, model.UseEnd);
                await _context.SaveChangesAsync();

                // 有駕駛 → 建立 Dispatch
                if (model.DriverId.HasValue)
                {
                    var dispatch = new Cars.Models.Dispatch
                    {
                        ApplyId = model.ApplyId,
                        DriverId = model.DriverId.Value,
                        VehicleId = model.VehicleId.Value,
                        DispatchStatus = "執勤中",
                        DispatchTime = DateTime.Now,
                        StartTime = model.UseStart,
                        EndTime = model.UseEnd,
                        CreatedAt = DateTime.Now
                    };
                    _context.Dispatches.Add(dispatch);
                    await _context.SaveChangesAsync();
                }
            }
            else if (model.PurposeType == "公務車(不可選車)")
            {
                
                await _context.SaveChangesAsync();
            }

            // 搭乘人員
            if (dto.Passengers != null && dto.Passengers.Any())
            {
                foreach (var p in dto.Passengers)
                {
                    p.ApplyId = model.ApplyId;
                    _context.CarPassengers.Add(p);
                }
                await _context.SaveChangesAsync();
            }

            // 自動派工
            if (model.PurposeType == "公務車(不可選車)")
            {
                var result = await dispatcher.AssignAsync(
                    model.ApplyId,
                    model.UseStart,
                    model.UseEnd,
                    model.PassengerCount,
                    model.VehicleType
                   
                );

                if (!result.Success)
                {
                    return Ok(new { message = $"申請成功，但派工失敗：{result.Message}", id = model.ApplyId });
                }
                model.DriverId = result.DriverId;
                model.VehicleId = result.VehicleId;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"申請與派車完成，司機：{result.DriverName}，車牌：{result.PlateNo}",
                    id = model.ApplyId,
                    driverId = result.DriverId,
                    vehicleId = result.VehicleId
                });
            }

            // 可選車 / 其他
            string vehiclePlate = null;
            string driverName = null;

            if (model.VehicleId.HasValue)
            {
                vehiclePlate = await _context.Vehicles
                    .Where(v => v.VehicleId == model.VehicleId.Value)
                    .Select(v => v.PlateNo)
                    .FirstOrDefaultAsync();
            }

            if (model.DriverId.HasValue)
            {
                driverName = await _context.Drivers
                    .Where(d => d.DriverId == model.DriverId.Value)
                    .Select(d => d.DriverName)
                    .FirstOrDefaultAsync();
            }

            string msg;
            if (model.PurposeType == "公務車(可選車)")
            {
                if (model.DriverId.HasValue)
                    msg = $"申請成功（已選車：{vehiclePlate}，駕駛：{driverName}）";
                else
                    msg = $"申請成功（已選車：{vehiclePlate}，未找到當下駕駛）";
            }
            else
            {
                msg = "申請成功";
            }

            return Ok(new
            {
                message = msg,
                id = model.ApplyId,
                vehicleId = model.VehicleId,
                driverId = model.DriverId
            });

        }

        // 取得全部申請單
        [HttpGet]
        public async Task<IActionResult> GetAll(
    [FromQuery] DateTime? dateFrom,
    [FromQuery] DateTime? dateTo,
    [FromQuery] string? q)
        {
            var query = _context.CarApplications
                .Include(a => a.Vehicle)
                .Include(a => a.Driver)   
                .AsNoTracking()
                .AsQueryable();

            if (dateFrom.HasValue)
                query = query.Where(a => a.UseStart >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                query = query.Where(a => a.UseStart < dateTo.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a =>
                    (a.ApplicantName ?? "").Contains(q) ||
                    (a.Origin ?? "").Contains(q) ||
                    (a.Destination ?? "").Contains(q));

            var list = await query
                .OrderByDescending(a => a.UseStart)
                .Select(a => new {
                    a.ApplyId,
                    a.ApplicantName,
                    a.ApplicantDept,
                    a.UseStart,
                    a.UseEnd,
                    a.Origin,
                    a.Destination,
                    a.PassengerCount,
                    a.TripType,
                    a.SingleDistance,
                    a.RoundTripDistance,
                    a.Status,

                    // 事由
                    a.ReasonType,
                    a.ApplyReason,

                    // 車輛
                    PlateNo = a.Vehicle != null ? a.Vehicle.PlateNo : null,

                    // 駕駛人
                    DriverName = a.Driver != null ? a.Driver.DriverName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // 取得單筆申請單 + 搭乘人員
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var app = await _context.CarApplications
                .Include(a => a.Vehicle)
                .Include(a => a.Driver)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ApplyId == id);
            if (app == null) return NotFound();

            var passengers = await _context.CarPassengers
                .Where(p => p.ApplyId == id)
                .ToListAsync();

            return Ok(new
            {
                app,
                passengers,
                driverName = app.Driver != null ? app.Driver.DriverName : null,
                driverId = app.DriverId,
                plateNo = app.Vehicle != null ? app.Vehicle.PlateNo : null,
                vehicleId = app.VehicleId
            });
        }
        public class StatusDto { public string? Status { get; set; } }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest("Status 不可為空");

            var app = await _context.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            app.Status = dto.Status.Trim();
            await _context.SaveChangesAsync();

            return Ok(new { message = "狀態已更新", status = app.Status });
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CarApply model)
        {
            var app = await _context.CarApplications.FindAsync(id);
            if (app == null) return NotFound();


            // 基本驗證（必要可再加強）
            if (model.UseStart == default(DateTime) || model.UseEnd == default(DateTime))
                return BadRequest("起訖時間不得為空");
            if (model.UseEnd <= model.UseStart)
                return BadRequest("結束時間必須晚於起始時間");


            // 欄位覆寫（與你的資料表對齊）
            app.ApplicantName = model.ApplicantName;
            app.ApplicantEmpId = model.ApplicantEmpId;
            app.ApplicantDept = model.ApplicantDept;
            app.ApplicantExt = model.ApplicantExt;
            app.ApplicantEmail = model.ApplicantEmail;
            app.ApplyFor = model.ApplyFor;
            app.VehicleType = model.VehicleType;
            app.PurposeType = model.PurposeType;
            app.VehicleId = model.VehicleId; // 可為 null


            app.PassengerCount = model.PassengerCount; // 若 DB 允許 NULL，建議把 Model 改成 int?
            app.UseStart = model.UseStart;
            app.UseEnd = model.UseEnd;


            app.DriverId = model.DriverId; // 可為 null
            app.ReasonType = model.ReasonType;
            app.ApplyReason = model.ApplyReason;


            app.Origin = model.Origin; // 建議不可為 null
            app.Destination = model.Destination; // 建議不可為 null


            app.TripType = model.TripType;
            app.SingleDistance = model.SingleDistance;
            app.SingleDuration = model.SingleDuration;
            app.RoundTripDistance = model.RoundTripDistance;
            app.RoundTripDuration = model.RoundTripDuration;


            app.Status = string.IsNullOrWhiteSpace(model.Status) ? app.Status : model.Status;


            await _context.SaveChangesAsync();
            return Ok(new { message = "更新成功" });
        }

        // 過濾可用車輛（排除 使用中 / 維修中 / 該時段已被派工）
        [HttpGet("/api/vehicles/available")]
        public async Task<IActionResult> GetAvailableVehicles(DateTime from, DateTime to, int? capacity = null)
        {
            if (from == default || to == default || to <= from)
                return BadRequest("時間區間不正確");

            var q = _context.Vehicles.AsQueryable();

            // 只要「可用」的車（排除 使用中 / 維修中）
            q = q.Where(v => (v.Status ?? "") == "可用");

            // 容量需要可承載（若你有這需求）
            if (capacity.HasValue)
                q = q.Where(v => v.Capacity >= capacity.Value);
            // 避開該時段已被派工的車
            q = q.Where(v => !_context.Dispatches.Any(d =>
                d.VehicleId == v.VehicleId &&
                from < d.EndTime &&
                d.StartTime < to));

            var list = await q
                .OrderBy(v => v.PlateNo)
                .Select(v => new {
                    v.VehicleId,
                    v.PlateNo,
                    v.Brand,
                    v.Model,
                    v.Capacity,
                    v.Status
                })
                .ToListAsync();

            return Ok(list);
        }
        //過濾可用司機
        [HttpGet("/api/drivers/available")]
        public async Task<IActionResult> GetAvailableDrivers(DateTime from, DateTime to)
        {
            if (from == default || to == default || to <= from)
                return BadRequest("時間區間不正確");

            var q = _context.Drivers.AsQueryable();

            // 避開該時段已被派工的駕駛
            q = q.Where(d => !_context.Dispatches.Any(dispatch =>
                dispatch.DriverId == d.DriverId &&
                from < dispatch.EndTime &&
                dispatch.StartTime < to));

            var list = await q
                .OrderBy(d => d.DriverName)
                .Select(d => new {
                    d.DriverId,
                    d.DriverName
                })
                .ToListAsync();

            return Ok(list);
        }
        // CarApplicationsController 內
        public class AssignDto { public int? DriverId { get; set; } public int? VehicleId { get; set; } }

        [HttpPatch("{id}/assignment")]
        public async Task<IActionResult> UpdateAssignment(int id, [FromBody] AssignDto dto)
        {
            var app = await _context.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            var from = app.UseStart; var to = app.UseEnd;

            // 驗證車在區間內可用（避免撞單）
            if (dto.VehicleId.HasValue)
            {
                var vUsed = await _context.Dispatches.AnyAsync(d =>
                    d.VehicleId == dto.VehicleId &&
                    d.ApplyId != id &&
                    from < d.EndTime && d.StartTime < to);
                if (vUsed) return BadRequest("該車於此時段已被派用");
            }

            // 驗證駕駛在區間內可用
            if (dto.DriverId.HasValue)
            {
                var dUsed = await _context.Dispatches.AnyAsync(d =>
                    d.DriverId == dto.DriverId &&
                    d.ApplyId != id &&
                    from < d.EndTime && d.StartTime < to);
                if (dUsed) return BadRequest("該駕駛於此時段已有派工");
            }

            app.DriverId = dto.DriverId;
            app.VehicleId = dto.VehicleId;

            // 同步 Dispatch（有就改，沒有就建）
            var disp = await _context.Dispatches.FirstOrDefaultAsync(d => d.ApplyId == id);
            if (disp == null && (dto.DriverId.HasValue || dto.VehicleId.HasValue))
            {
                _context.Dispatches.Add(new Cars.Models.Dispatch
                {
                    ApplyId = id,
                    DriverId = dto.DriverId ?? 0,
                    VehicleId = dto.VehicleId ?? 0,
                    DispatchStatus = "執勤中",
                    DispatchTime = DateTime.Now,
                    StartTime = from,
                    EndTime = to,
                    CreatedAt = DateTime.Now
                });
            }
            else if (disp != null)
            {
                if (dto.DriverId.HasValue) disp.DriverId = dto.DriverId.Value;
                if (dto.VehicleId.HasValue) disp.VehicleId = dto.VehicleId.Value;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "指派已更新" });
        }


        // 刪除申請單（連同搭乘人員）
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var app = await _context.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            // 刪掉子表（派工單）
            var dispatches = _context.Dispatches.Where(d => d.ApplyId == id);
            _context.Dispatches.RemoveRange(dispatches);

            // 刪掉子表（乘客）
            var passengers = _context.CarPassengers.Where(p => p.ApplyId == id);
            _context.CarPassengers.RemoveRange(passengers);

            // 最後刪掉申請單
            _context.CarApplications.Remove(app);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "刪除成功" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "刪除失敗", detail = ex.Message });
            }
        }


        [HttpGet("Search")]
        public async Task<IActionResult> Search(
            string origin,
            string destination,
            [FromServices] PlaceAliasService aliasService)
        {
            var q = _context.CarApplications.AsQueryable();

            if (!string.IsNullOrWhiteSpace(origin))
            {
                var realOrigin = await aliasService.ResolveAsync(origin);
                q = q.Where(a => a.Origin.Contains(realOrigin));
            }

            if (!string.IsNullOrWhiteSpace(destination))
            {
                var realDest = await aliasService.ResolveAsync(destination);
                q = q.Where(a => a.Destination.Contains(realDest));
            }

            var list = await q
                .OrderByDescending(a => a.ApplyId)
                .ToListAsync();

            return Ok(list);
        }
    }
}
