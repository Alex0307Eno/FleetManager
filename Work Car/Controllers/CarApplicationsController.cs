using Microsoft.AspNetCore.Mvc;
using Cars.Data;
using Cars.Models;
using Cars.Services;
using Microsoft.EntityFrameworkCore;
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
            model.DriverId = null;

            _context.CarApplications.Add(model);
            await _context.SaveChangesAsync(); // 先存申請單，取得 ApplyId

            // 存搭乘人員（如果有傳）
            if (dto.Passengers != null && dto.Passengers.Any())
            {
                foreach (var p in dto.Passengers)
                {
                    p.ApplyId = model.ApplyId; // 綁申請單
                    _context.CarPassengers.Add(p);
                }
                await _context.SaveChangesAsync();
            }

            // ✅ 如果是公務車(不可選車)，自動派工
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
                    return Ok(new
                    {
                        message = $"申請成功，但派工失敗：{result.Message}",
                        id = model.ApplyId
                    });
                }

                return Ok(new
                {
                    message = $"申請與派車完成，司機：{result.DriverName}，車牌：{result.PlateNo}",
                    id = model.ApplyId,
                    driverId = result.DriverId,
                    vehicleId = result.VehicleId
                });
            }

            return Ok(new { message = "申請成功", id = model.ApplyId });
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
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] CarApply model)
        {
            var app = _context.CarApplications.Find(id);
            if (app == null) return NotFound();

            // 更新欄位
            app.ApplicantName = model.ApplicantName;
            app.ApplicantEmpId = model.ApplicantEmpId;
            app.ApplicantDept = model.ApplicantDept;
            app.ApplicantExt = model.ApplicantExt;
            app.ApplicantEmail = model.ApplicantEmail;
            app.ApplyFor = model.ApplyFor;
            app.VehicleType = model.VehicleType;
            app.PurposeType = model.PurposeType;
            app.PassengerCount = model.PassengerCount;
            app.UseStart = model.UseStart;
            app.UseEnd = model.UseEnd;
            app.ReasonType = model.ReasonType;
            app.ApplyReason = model.ApplyReason;
            app.Origin = model.Origin;
            app.Destination = model.Destination;
            app.TripType = model.TripType;
            app.SingleDistance = model.SingleDistance;
            app.SingleDuration = model.SingleDuration;
            app.RoundTripDistance = model.RoundTripDistance;
            app.RoundTripDuration = model.RoundTripDuration;

            _context.SaveChanges();
            return Ok(new { message = "更新成功" });
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
    }
}
