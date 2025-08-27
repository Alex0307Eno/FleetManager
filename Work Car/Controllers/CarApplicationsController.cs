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

        private static bool IsOverlap(DateTime s1, DateTime e1, DateTime s2, DateTime e2)
            => s1 < e2 && s2 < e1;

        [HttpPost]
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

            // 預設由系統指派
            model.DriverId = null;

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
                model.VehicleId = null;
                model.DriverId = null;
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

                return Ok(new
                {
                    message = $"申請與派車完成，司機：{result.DriverName}，車牌：{result.PlateNo}",
                    id = model.ApplyId,
                    driverId = result.DriverId,
                    vehicleId = result.VehicleId
                });
            }

            // 可選車 / 其他
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
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.CarApplications.ToListAsync();
            return Ok(list);
        }

        // 取得單筆申請單 + 搭乘人員
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var app = await _context.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            var passengers = await _context.CarPassengers
                .Where(p => p.ApplyId == id)
                .ToListAsync();

            return Ok(new { app, passengers });
        }

        // 更新申請單（不含搭乘人員）
        //[HttpPut("{id}")]
        //public async Task<IActionResult> Update(int id, [FromBody] CarApply model, [FromServices] AutoDispatcher dispatcher)
        //{
        //    var app = await _context.CarApplications.FindAsync(id);
        //    if (app == null) return NotFound();

        //    if (model.UseStart == default || model.UseEnd == default)
        //        return BadRequest("起訖時間不得為空");
        //    if (model.UseEnd <= model.UseStart)
        //        return BadRequest("結束時間必須晚於起始時間");

        //    // ★ 可選車：同樣檢查 + 重新計算 DriverId
        //    if (model.PurposeType == "公務車(可選車)")
        //    {
        //        if (model.VehicleId == null) return BadRequest("請選擇車輛");

        //        var vehicle = await _context.Vehicles
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(v => v.VehicleId == model.VehicleId.Value);
        //        if (vehicle == null) return BadRequest("車輛不存在");

        //        if ((vehicle.Status ?? "").Trim() != "可用")
        //            return BadRequest("該車輛目前不可用，請重新選擇");

        //        var vUsed = await _context.Dispatches.AnyAsync(d =>
        //            d.VehicleId == model.VehicleId &&
        //            d.ApplyId != id &&                      // 若有以 ApplyId 關聯，避免算到自己（視你的資料設計）
        //            model.UseStart < d.EndTime &&
        //            d.StartTime < model.UseEnd);
        //        if (vUsed) return BadRequest("該車於申請時段已被派用，請改選其他車或調整時間");

        //        model.DriverId = await dispatcher.FindOnDutyDriverIdAsync(model.UseStart, model.UseEnd);
        //    }
        //    else if (model.PurposeType == "公務車(不可選車)")
        //    {
        //        model.VehicleId = null;
        //        model.DriverId = null;
        //    }


        //    // 更新欄位
        //    app.ApplicantName = model.ApplicantName;
        //    app.ApplicantEmpId = model.ApplicantEmpId;
        //    app.ApplicantDept = model.ApplicantDept;
        //    app.ApplicantExt = model.ApplicantExt;
        //    app.ApplicantEmail = model.ApplicantEmail;
        //    app.ApplyFor = model.ApplyFor;
        //    app.VehicleType = model.VehicleType;
        //    app.PurposeType = model.PurposeType;
        //    app.PassengerCount = model.PassengerCount;
        //    app.UseStart = model.UseStart;
        //    app.UseEnd = model.UseEnd;
        //    app.ReasonType = model.ReasonType;
        //    app.ApplyReason = model.ApplyReason;
        //    app.Origin = model.Origin;
        //    app.Destination = model.Destination;
        //    app.TripType = model.TripType;
        //    app.SingleDistance = model.SingleDistance;
        //    app.SingleDuration = model.SingleDuration;
        //    app.RoundTripDistance = model.RoundTripDistance;
        //    app.RoundTripDuration = model.RoundTripDuration;
        //    app.VehicleId = model.VehicleId;    
        //    app.DriverId = model.DriverId;     

        //    await _context.SaveChangesAsync();
        //    return Ok(new { message = "更新成功" });
        //}

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

        // 刪除申請單（連同搭乘人員）
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var app = await _context.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            var passengers = _context.CarPassengers.Where(p => p.ApplyId == id);
            _context.CarPassengers.RemoveRange(passengers);

            _context.CarApplications.Remove(app);
            await _context.SaveChangesAsync();

            return Ok(new { message = "刪除成功" });
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
