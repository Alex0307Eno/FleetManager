using Cars.Data;
using Cars.Models;
using DocumentFormat.OpenXml.Wordprocessing;
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
                 .Where(d => !d.IsAgent)
                .OrderBy(d => d.DriverName )
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
                        s.IsPresent == true),

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

                // 2) 讀取今天的代理紀錄
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

                    var isAgent = await _db.Drivers
                     .Where(x => x.DriverId == agentDriverId)
                     .Select(x => x.IsAgent)
                     .FirstOrDefaultAsync();

                    if (!isAgent)
                        return StatusCode(409, "所選代理人不是代理人員（IsAgent=false）");


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


        //自動指派代理人
        private async Task<int> PickAutoAgent(int principalDriverId, string? shift, DateTime today)
        {
            var now = DateTime.Now;

            // 候選：今天有出勤、而且是代理人(IsAgent=true)；排除本人
            var agents = await _db.Drivers
            .Where(d => d.IsAgent && d.DriverId != principalDriverId)
            .Select(d => d.DriverId)
            .ToListAsync();

            if (agents.Count == 0) return 0;

            // 今天已在代理別人的人（避免一人代理多人）
            var busyAgentIds = await _db.DriverDelegations
                .Where(g => g.StartDate.Date <= today && today <= g.EndDate.Date)
                .Select(g => g.AgentDriverId)
                .Distinct()
                .ToListAsync();

            // 正在執勤的人
            var onDutyIds = await _db.Dispatches
                .Where(dis => dis.StartTime <= now && now <= dis.EndTime)
                .Select(dis => dis.DriverId ?? 0)
                .Distinct()
                .ToListAsync();

            // 長差剛回來未滿 1 小時的人
            var restIds = await _db.Dispatches
                .Where(dis => dis.IsLongTrip && dis.EndTime != null &&
                              dis.EndTime > now.AddHours(-1) && dis.EndTime <= now)
                .Select(dis => dis.DriverId ?? 0)
                .Distinct()
                .ToListAsync();

            var pool = agents
         .Where(id => !busyAgentIds.Contains(id)
                   && !onDutyIds.Contains(id)
                   && !restIds.Contains(id))
         .ToList();

            if (!pool.Any()) return 0;

            // 今天派工數少者優先
            var todayCounts = await _db.Dispatches
                .Where(dis => dis.StartTime.HasValue && dis.StartTime.Value.Date == today)
                .GroupBy(dis => dis.DriverId)
                .Select(g => new { DriverId = g.Key ?? 0, Cnt = g.Count() })
                .ToDictionaryAsync(x => x.DriverId, x => x.Cnt);

            return pool
                .OrderBy(id => todayCounts.ContainsKey(id) ? todayCounts[id] : 0)
                .ThenBy(id => id)
                .First();
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
            .FirstOrDefaultAsync(d => d.DriverId == id && d.IsAgent == false);


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
                    title = s.Shift,         
                    start = s.WorkDate,       
                    end = s.WorkDate.AddDays(1),       
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
