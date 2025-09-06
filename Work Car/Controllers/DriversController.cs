using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [Authorize]
    [Route("Drivers")]

    public class DriversController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DriversController(ApplicationDbContext db) => _db = db;

        [HttpGet("Records")]
        public async Task<ActionResult<IEnumerable<DriverListItem>>> Records()
        {
            var today = DateTime.Today;

            var data = await _db.Drivers
                .AsNoTracking()
                .OrderBy(d => d.DriverName)
                .Select(d => new DriverListItem
                {
                    DriverId = d.DriverId,
                    DriverName = d.DriverName,

                    NationalId = d.NationalId,
                    BirthDate = d.BirthDate,
                    HouseholdAddress = d.HouseholdAddress,
                    ContactAddress = d.ContactAddress,
                    Phone = d.Phone,
                    Mobile = d.Mobile,
                    EmergencyContactName = d.EmergencyContactName,
                    EmergencyContactPhone = d.EmergencyContactPhone,

                    HasTodaySchedule = _db.Schedules.Any(s =>
                        s.DriverId == d.DriverId &&
                        s.WorkDate == today),

                    IsPresentToday = _db.Schedules.Any(s =>
                        s.DriverId == d.DriverId &&
                        s.WorkDate == today &&
                        s.IsPresent == true)
                })
                .ToListAsync();

            return Ok(data);
        }
        public class SetAttendanceDto
        {
            public int DriverId { get; set; }
            public bool IsPresent { get; set; }
            public int? AgentId { get; set; }      //  請假時指派代理人
            public string? Reason { get; set; }    // 例如 "請假"
        }

        // 標記今天的出勤狀況
        [HttpPost("SetAttendanceToday")]
        public async Task<IActionResult> SetAttendanceToday([FromBody] SetAttendanceDto dto)
        {
            // 以台灣時區計算今日
            var today = DateTime.UtcNow.AddHours(8).Date;

            var sched = await _db.Schedules
                .FirstOrDefaultAsync(s => s.WorkDate == today && s.DriverId == dto.DriverId);
            if (sched == null)
                return BadRequest("今日沒有班表記錄");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1) 更新出勤
                sched.IsPresent = dto.IsPresent;

                // 2) 讀取今天的代理紀錄（以 PrincipalDriverId 鎖定）
                var existing = await _db.DriverDelegations
                    .FirstOrDefaultAsync(d =>
                        d.PrincipalDriverId == dto.DriverId &&
                        d.StartDate.Date <= today && today <= d.EndDate.Date);

                if (!dto.IsPresent)
                {
                    // 3) 自動挑選可用的「代理司機」（回傳的是 Drivers.DriverId）
                    var agentDriverId = await PickAutoAgent(dto.DriverId, sched.Shift, today);
                    if (agentDriverId == 0)
                        return StatusCode(409, "沒有可用的代理人");
                    if (agentDriverId == dto.DriverId)
                        return StatusCode(409, "代理人不可與本人相同");

                    // 4) Upsert 今天的代理關係（⚠️ 寫入 AgentDriverId）
                    if (existing == null)
                    {
                        _db.DriverDelegations.Add(new DriverDelegation
                        {
                            PrincipalDriverId = dto.DriverId,
                            AgentDriverId = agentDriverId,                   
                            StartDate = today,
                            EndDate = today,
                            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "系統自動指派" : dto.Reason
                        });
                    }
                    else
                    {
                        existing.AgentDriverId = agentDriverId;               
                        existing.Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "系統自動指派" : dto.Reason;
                        existing.StartDate = today;
                        existing.EndDate = today;
                        _db.DriverDelegations.Update(existing);
                    }
                }
                else
                {
                    // 5) 取消請假 → 移除當天代理（若你允許跨天可改成只縮短日期）
                    if (existing != null)
                        _db.DriverDelegations.Remove(existing);
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return NoContent();
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, ex.InnerException?.Message ?? ex.Message);
            }
        }


        /// <summary>
        /// 依規則挑代理人：
        /// 1. 優先找「同班別」且今天出勤的人
        /// 2. 退而求其次，找今天有班表且出勤的人
        /// 3. 再不行，找任何一個不同於本人的 Driver
        /// 回傳 0 表示找不到
        /// </summary>
        private async Task<int> PickAutoAgent(int absentDriverId, string? shift, DateTime today)
        {
            // 1) 同班別且出勤
            if (!string.IsNullOrWhiteSpace(shift))
            {
                var sameShift = await _db.Schedules
                    .Where(s => s.WorkDate == today
                                && s.Shift == shift
                                && s.DriverId != absentDriverId
                                && s.IsPresent)
                    .Select(s => s.DriverId)
                    .FirstOrDefaultAsync();
                if (sameShift != 0) return sameShift;
            }

            // 2) 任一出勤的司機
            var anyPresent = await _db.Schedules
                .Where(s => s.WorkDate == today
                            && s.DriverId != absentDriverId
                            && s.IsPresent)
                .Select(s => s.DriverId)
                .FirstOrDefaultAsync();
            if (anyPresent != 0) return anyPresent;

            // 3) 任一司機（最後退路）
            var fallback = await _db.Drivers
                .Where(d => d.DriverId != absentDriverId)
                .Select(d => d.DriverId)
                .FirstOrDefaultAsync();
            return fallback; // 若完全沒有其他司機，這裡會回 0
        }



        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await _db.Drivers
                .AsNoTracking()
                .OrderBy(d => d.DriverName)
                .ToListAsync();
            return View(list);
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> DetailsApi(int id)
        {
            var driver = await _db.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DriverId == id);

            if (driver == null) return NotFound();

            return Json(driver);
        }


        [HttpGet("Create")]
        public IActionResult Create() => View();

        // POST: /Drivers/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DriverName,NationalId,BirthDate,HouseholdAddress,ContactAddress,Phone,Mobile,EmergencyContactName,EmergencyContactPhone")] Driver input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            // 例：若需要檢查身分證是否重複
            if (!string.IsNullOrWhiteSpace(input.NationalId))
            {
                var exists = await _db.Drivers.AnyAsync(x => x.NationalId == input.NationalId);
                if (exists)
                {
                    ModelState.AddModelError(nameof(input.NationalId), "此身分證字號已存在。");
                    return View(input);
                }
            }

            _db.Drivers.Add(input);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var d = await _db.Drivers.FindAsync(id);
            if (d == null) return NotFound();
            return View(d);
        }

        // POST: /Drivers/Edit/5
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DriverId,DriverName,NationalId,BirthDate,HouseholdAddress,ContactAddress,Phone,Mobile,EmergencyContactName,EmergencyContactPhone")] Driver input)
        {
            if (id != input.DriverId) return BadRequest();

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var entity = await _db.Drivers.FirstOrDefaultAsync(x => x.DriverId == id);
            if (entity == null) return NotFound();

            // （如需檢查 NationalId 是否與其他人重複）
            if (!string.IsNullOrWhiteSpace(input.NationalId))
            {
                var exists = await _db.Drivers
                    .AnyAsync(x => x.NationalId == input.NationalId && x.DriverId != id);
                if (exists)
                {
                    ModelState.AddModelError(nameof(input.NationalId), "此身分證字號已存在於其他駕駛。");
                    return View(input);
                }
            }

            // 僅更新允許的欄位（避免 Overposting）
            entity.DriverName = input.DriverName;
            entity.NationalId = input.NationalId;
            entity.BirthDate = input.BirthDate;
            entity.HouseholdAddress = input.HouseholdAddress;
            entity.ContactAddress = input.ContactAddress;
            entity.Phone = input.Phone;
            entity.Mobile = input.Mobile;
            entity.EmergencyContactName = input.EmergencyContactName;
            entity.EmergencyContactPhone = input.EmergencyContactPhone;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        // 司機自己的班表頁
        [Authorize(Roles = "Driver")]
        [HttpGet("MySchedule")] 
        public IActionResult MySchedule() => View();

        [Authorize(Roles = "Driver")]
        [HttpGet("MySchedule/Events")]
        public async Task<IActionResult> MyScheduleEvents(DateTime? start, DateTime? end)
        {
            var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidStr, out var userId))
                return Forbid();

            // 從 Drivers 表找到對應的 DriverId
            var myDriverId = await _db.Drivers
                .AsNoTracking()
                .Where(d => d.UserId == userId)       // ⚠️ 這裡用 Driver.UserId
                .Select(d => (int?)d.DriverId)
                .FirstOrDefaultAsync();

            if (myDriverId == null || myDriverId == 0)
                return Forbid(); // 這個使用者沒有綁定司機

            // 查 Schedules 表，只抓自己的班表
            var q = _db.Schedules
                .AsNoTracking()
                .Where(s => s.DriverId == myDriverId.Value);

            if (start.HasValue) q = q.Where(s => s.WorkDate >= start.Value);
            if (end.HasValue) q = q.Where(s => s.WorkDate <= end.Value);

            var events = await q
                .OrderBy(s => s.WorkDate)
                .Select(s => new {
                    id = s.ScheduleId,
                    title = s.Shift,          // 直接顯示班別 (早班/午班…)
                    start = s.WorkDate,       // FullCalendar 會自動轉 ISO
                    end = s.WorkDate,       // 如果 Shift 沒有結束時間，就先用同一天
                    extendedProps = new
                    {
                        driverId = s.DriverId
                    }
                })
                .ToListAsync();

            return Ok(events);
        }

        // 出勤
        public class DriverListItem
        {
            public int DriverId { get; set; }
            public string DriverName { get; set; }

            public string NationalId { get; set; }
            public DateTime? BirthDate { get; set; }
            public string HouseholdAddress { get; set; }
            public string ContactAddress { get; set; }
            public string Phone { get; set; }
            public string Mobile { get; set; }
            public string EmergencyContactName { get; set; }
            public string EmergencyContactPhone { get; set; }

            // 出勤顯示/控制
            public bool IsPresentToday { get; set; }   // 今天是否出勤
            public bool HasTodaySchedule { get; set; } // 今天是否有排班
        }

    }
}
