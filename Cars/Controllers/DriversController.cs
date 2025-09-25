using Cars.Data;
using Cars.Features.Drivers;
using Cars.Models;
using Cars.Services;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


namespace Cars.ApiControllers
{
    [Authorize]
    [Route("Drivers")]

    public class DriversController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly DriverService _driverService;
        public DriversController(ApplicationDbContext db, DriverService driverService) 
        {
            _db = db;
            _driverService = driverService;
        } 


        #region 班表管理頁
        // 管理端：查所有/指定司機的班表
        [Authorize(Roles = "Admin")]
        [HttpGet("Schedule/Events")]
        public async Task<IActionResult> GetSchedules(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var list = await (
                from s in _db.Schedules.AsNoTracking()
                where s.WorkDate >= start && s.WorkDate <= end
                join dla in _db.DriverLineAssignments.AsNoTracking()
                     on s.LineCode equals dla.LineCode into gj
                from dla in gj.Where(a => a.StartDate <= s.WorkDate && (a.EndDate == null || a.EndDate >= s.WorkDate))
                              .DefaultIfEmpty()
                let resolvedDriverId = (int?)(s.DriverId ?? (dla != null ? dla.DriverId : (int?)null))
                join d in _db.Drivers.AsNoTracking()
                     on resolvedDriverId equals (int?)d.DriverId into gd
                from d in gd.DefaultIfEmpty()
                select new
                {
                    scheduleId = s.ScheduleId,
                    workDate = s.WorkDate,
                    shift = s.Shift,
                    lineCode = s.LineCode,
                    driverId = resolvedDriverId,
                    driverName = d != null ? d.DriverName : null,
                    isPresent = s.IsPresent
                }
            )
            // 固定排序：日期 → 班別(AM,PM,G1,G2,G3) → LineCode(A..E)
            .OrderBy(x => x.workDate)
            .ThenBy(x => x.shift == "AM" ? 1 :
                         x.shift == "PM" ? 2 :
                         x.shift == "G1" ? 3 :
                         x.shift == "G2" ? 4 : 5)
            .ThenBy(x => x.lineCode)  // A → E
            .ToListAsync();

            return Ok(list);
        }


        #endregion

        #region 更新班表

        // DTO
        public class BulkSetDto
        {
            public List<DateTime> Dates { get; set; } = new();  // 要套用的日期清單（日期即可）
            public PersonAssign Assign { get; set; } = new();    // A~E 對應 AM/PM/G1/G2/G3
        }
        public class PersonAssign
        {
            public int? A { get; set; }  // 人
            public int? B { get; set; }
            public int? C { get; set; }
            public int? D { get; set; }
            public int? E { get; set; }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Schedule/BulkSet")]
        public async Task<IActionResult> BulkSet([FromBody] BulkSetDto dto)
        {
            if (dto == null || dto.Dates == null || dto.Dates.Count == 0)
                return BadRequest("沒有日期");

            // A~E → DriverId (nullable)
            var driverMap = new Dictionary<string, int?>
            {
                ["A"] = dto.Assign.A,
                ["B"] = dto.Assign.B,
                ["C"] = dto.Assign.C,
                ["D"] = dto.Assign.D,
                ["E"] = dto.Assign.E
            };

            // 驗證 DriverId 是否存在
            var driverIds = driverMap.Values.Where(v => v.HasValue).Select(v => v.Value).ToList();
            if (driverIds.Count > 0)
            {
                var valid = await _db.Drivers.Where(d => driverIds.Contains(d.DriverId))
                                             .Select(d => d.DriverId)
                                             .ToListAsync();
                var invalid = driverIds.Except(valid).ToList();
                if (invalid.Any())
                    return BadRequest("含無效 DriverId：" + string.Join(",", invalid));
            }

            // DriverId → DriverName 字典
            var driverDict = await _db.Drivers
                .AsNoTracking()
                .ToDictionaryAsync(d => d.DriverId, d => d.DriverName);

            var shifts = new[] { "AM", "PM", "G1", "G2", "G3" };
            var changes = new List<object>();

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var date in dto.Dates.Select(x => x.Date).Distinct())
                {
                    // 一次抓出當天 5 筆
                    var dayRows = await _db.Schedules
                        .Where(s => s.WorkDate == date)
                        .ToListAsync();

                    // 僅處理「已存在的班別」，不新增額外筆數
                    foreach (var shift in shifts)
                    {
                        var row = dayRows.FirstOrDefault(r => r.Shift == shift);
                        if (row == null) continue; // 沒有這個班別的骨架就跳過

                        var line = row.LineCode;               
                        var newDriverId = driverMap.ContainsKey(line) ? driverMap[line] : null;

                        if (newDriverId.HasValue)
                        {
                            if (!row.DriverId.HasValue || row.DriverId.Value != newDriverId.Value)
                            {
                                var oldName = row.DriverId.HasValue && driverDict.TryGetValue(row.DriverId.Value, out var on) ? on : null;
                                var newName = driverDict.TryGetValue(newDriverId.Value, out var nn) ? nn : null;
                                var oldDriverId = row.DriverId;
                                row.DriverId = newDriverId.Value;
                                row.IsPresent = true;
                                _db.Schedules.Update(row);

                                changes.Add(new
                                {
                                    Date = date.ToString("yyyy-MM-dd"),
                                    Shift = shift,
                                    LineCode = line,
                                    OldDriver = oldName,
                                    NewDriver = newName,
                                    Action = row.DriverId.HasValue ? "Update" : "Insert"
                                });
                                // 新增代理紀錄
                                if (oldDriverId.HasValue && oldDriverId.Value != newDriverId.Value)
                                {
                                    var newDriver = await _db.Drivers.FindAsync(newDriverId.Value);
                                    if (newDriver != null && newDriver.IsAgent)
                                    {
                                        // 先檢查有沒有已存在的紀錄
                                        bool exists = await _db.DriverDelegations.AnyAsync(d =>
                                            d.PrincipalDriverId == oldDriverId.Value &&
                                            d.AgentDriverId == newDriverId.Value &&
                                            d.StartDate.Date == date.Date);

                                        if (!exists)
                                        {
                                            var delegation = new DriverDelegation
                                            {
                                                PrincipalDriverId = oldDriverId.Value,
                                                AgentDriverId = newDriverId.Value,
                                                StartDate = date,
                                                EndDate = date,
                                                Reason = "請假",
                                                CreatedAt = DateTime.Now
                                            };
                                            _db.DriverDelegations.Add(delegation);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (row.DriverId.HasValue)
                            {
                                var oldName = driverDict.TryGetValue(row.DriverId.Value, out var on) ? on : null;

                                row.DriverId = null;            // 只清空 DriverId，不刪 row
                                row.IsPresent = false;
                                _db.Schedules.Update(row);

                                changes.Add(new
                                {
                                    Date = date.ToString("yyyy-MM-dd"),
                                    Shift = shift,
                                    LineCode = line,
                                    OldDriver = oldName,
                                    NewDriver = (string)null,
                                    Action = "Clear"
                                });
                            }
                        }
                    }
                }

                var (ok, err1) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err1!; 
                await tx.CommitAsync();

                return Json(changes);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }





        #endregion

        #region 設定歷史對應
        public class SetAssignmentDto
        {
            public DateTime StartDate { get; set; }
            public DateTime? EndDate { get; set; } // null=一直到未來
            public int? A { get; set; }
            public int? B { get; set; }
            public int? C { get; set; }
            public int? D { get; set; }
            public int? E { get; set; }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Schedule/SetLineAssignments")]
        public async Task<IActionResult> SetLineAssignments([FromBody] SetAssignmentDto dto)
        {
            var map = new Dictionary<string, int?>
            {
                ["A"] = dto.A,
                ["B"] = dto.B,
                ["C"] = dto.C,
                ["D"] = dto.D,
                ["E"] = dto.E
            };

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var kv in map)
                {
                    var line = kv.Key; var driverId = kv.Value;
                    if (!driverId.HasValue) continue;

                    // 關閉與這段期間重疊的舊對應（簡化處理：全部結束在 startDate 前一天）
                    var overlap = await _db.DriverLineAssignments
                        .Where(a => a.LineCode == line &&
                                    a.EndDate == null || a.EndDate >= dto.StartDate)
                        .ToListAsync();

                    foreach (var a in overlap)
                        a.EndDate = dto.StartDate.AddDays(-1);

                    // 新增這段期間的對應
                    _db.DriverLineAssignments.Add(new DriverLineAssignment
                    {
                        LineCode = line,
                        DriverId = driverId.Value,
                        StartDate = dto.StartDate.Date,
                        EndDate = dto.EndDate?.Date
                    });
                }

                var (ok, err1) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err1!; 
                await tx.CommitAsync();
                return NoContent();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        #endregion

        #region 寫入派工至班表
        // 取得某一天已核准的用車（派工）清單
        [HttpGet("Schedule/DayDispatches")]
        public async Task<IActionResult> DayDispatches(DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            var list = await _db.Dispatches
                .Include(d => d.Driver)
                .Where(d => d.StartTime >= start && d.StartTime < end)
                .OrderBy(d => d.StartTime)
                .Select(d => new
                {
                    id = d.DispatchId,
                    driverId = d.DriverId,
                    driverName = d.Driver != null ? d.Driver.DriverName : null,
                    start = d.StartTime,
                    end = d.EndTime,
                    shift = d.StartTime.Value.Hour < 12 ? "AM" : "PM" // 先簡單切早午
                                                                
                })
                .ToListAsync();

            return Ok(list);
        }

        #endregion


        #region 司機基本資料

        [HttpGet("Records")]
        public async Task<ActionResult> Records()
        {
            var today = DateTime.Today;

            var data = await _db.Drivers
                .AsNoTracking()
                 .Where(d => !d.IsAgent)
                .OrderBy(d => d.DriverName )
                .Select(d => new Driver
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

                  

                })
                .ToListAsync();

            return Ok(data);
        }
       

        #endregion

        

        #region 自動指派代理人(暫不使用)
        //自動指派代理人
        private async Task<int> PickAutoAgent(int principalDriverId, string shift, DateTime today)
        {
            var now = DateTime.Now;

            // 當天與昨天的日界線
            var dayStart = today.Date;
            var dayEnd = dayStart.AddDays(1);
            var yStart = dayStart.AddDays(-1);
            var yEnd = dayStart;

            // 候選：代理人，排除本人
            var agents = await _db.Drivers
                .Where(d => d.IsAgent && d.DriverId != principalDriverId)
                .Select(d => d.DriverId)
                .ToListAsync();

            if (agents.Count == 0) return 0;

            // 今天已在代理別人的人（避免一人代理多人）
            var busyAgentIds = await _db.DriverDelegations
                .Where(g => g.StartDate < dayEnd && g.EndDate >= dayStart) // 與今天有交集
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

            //  昨天替「這位本尊」有效的代理人
            var yesterdayForPrincipal = await _db.DriverDelegations
                .Where(g => g.PrincipalDriverId == principalDriverId &&
                            g.StartDate < yEnd && g.EndDate >= yStart) // 與昨天有交集
                .Select(g => g.AgentDriverId)
                .Distinct()
                .ToListAsync();

            //  昨天有出勤的代理人
            var workedYesterday = await _db.Dispatches
                .Where(dis => dis.StartTime >= yStart && dis.StartTime < yEnd)
                .Select(dis => dis.DriverId ?? 0)
                .Distinct()
                .ToListAsync();

            // 基本可用池
            var basePool = agents
                .Where(id => !busyAgentIds.Contains(id)
                          && !onDutyIds.Contains(id)
                          && !restIds.Contains(id))
                .ToList();

            // 先排除「昨天替同一位本尊」的代理人
            var pool = basePool
                .Where(id => !yesterdayForPrincipal.Contains(id))
                .ToList();

            // 若因此沒人，再排除「昨天有出勤」者
            if (pool.Count == 0)
                pool = basePool.Where(id => !workedYesterday.Contains(id)).ToList();

            // 再不行就用原本池（避免完全選不到）
            if (pool.Count == 0)
                pool = basePool;

            if (pool.Count == 0) return 0;

            // 今天派工數少者優先
            var todayCounts = await _db.Dispatches
                .Where(dis => dis.StartTime >= dayStart && dis.StartTime < dayEnd)
                .GroupBy(dis => dis.DriverId)
                .Select(g => new { DriverId = g.Key ?? 0, Cnt = g.Count() })
                .ToDictionaryAsync(x => x.DriverId, x => x.Cnt);

            // 排序：今日派工數 → 昨天是否出勤（出勤者往後）→ DriverId
            return pool
                .OrderBy(id => todayCounts.ContainsKey(id) ? todayCounts[id] : 0)
                .ThenBy(id => workedYesterday.Contains(id) ? 1 : 0)
                .ThenBy(id => id)
                .First();
        }
        #endregion

        #region 司機CRUD
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
        public async Task<IActionResult> Details(int id)
        {
            var driver = await _db.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DriverId == id && d.IsAgent == false);


            if (driver == null) return NotFound();

            return Json(driver);
        }


        [HttpGet("Create")]
        [ValidateAntiForgeryToken]
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
            var (ok, err1) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err1!; 
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

            // 僅更新允許的欄位
            entity.DriverName = input.DriverName;
            entity.NationalId = input.NationalId;
            entity.BirthDate = input.BirthDate;
            entity.HouseholdAddress = input.HouseholdAddress;
            entity.ContactAddress = input.ContactAddress;
            entity.Phone = input.Phone;
            entity.Mobile = input.Mobile;
            entity.EmergencyContactName = input.EmergencyContactName;
            entity.EmergencyContactPhone = input.EmergencyContactPhone;

            var (ok, err1) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err1!;
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region 司機個人班表
        // 司機自己的班表頁
        [Authorize(Roles = "Driver")]
        [HttpGet("MySchedule")] 
        public IActionResult MySchedule() => View();

        [Authorize(Roles = "Driver")]
        [HttpGet("MySchedule/Events")]
        public async Task<IActionResult> MyScheduleEvents(DateTime? start, DateTime? end)
        {
            var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidStr, out var userId)) return Forbid();

            var myDriverId = await _db.Drivers.AsNoTracking()
                .Where(d => d.UserId == userId)
                .Select(d => (int?)d.DriverId)
                .FirstOrDefaultAsync();

            if (myDriverId == null || myDriverId == 0) return Forbid();

            var q =
                from s in _db.Schedules.AsNoTracking()
                join dla in _db.DriverLineAssignments.AsNoTracking()
                     on s.LineCode equals dla.LineCode into gj
                from dla in gj.Where(a => a.StartDate <= s.WorkDate && (a.EndDate == null || a.EndDate >= s.WorkDate))
                              .DefaultIfEmpty()
                join d in _db.Drivers.AsNoTracking()
                     on (s.DriverId ?? (dla != null ? dla.DriverId : (int?)null)) equals d.DriverId
                where d.DriverId == myDriverId
                select new
                {
                    id = s.ScheduleId,
                    title = d.DriverName + 
                            (s.Shift == "AM" ? "早・午" :
                             s.Shift == "PM" ? "午・晚" :
                             s.Shift == "G1" ? "一般(1)" :
                             s.Shift == "G2" ? "一般(2)" :
                             s.Shift == "G3" ? "一般(3)" : s.Shift),
                    start = s.WorkDate,
                    end = s.WorkDate.AddDays(1),
                    shift = s.Shift,
                    lineCode = s.LineCode
                };


            if (start.HasValue) q = q.Where(x => x.start >= start.Value);
            if (end.HasValue) q = q.Where(x => x.start <= end.Value);

            var events = await q.OrderBy(x => x.start).ToListAsync();
            return Ok(events);
        }

        #endregion



        #region 代理人清單
        //代理人清單
        [HttpGet("Agents")]
        public async Task<IActionResult> GetAgents([FromQuery] int? leaveId = null)
        {
            var q = _db.Drivers.AsNoTracking().Where(d => d.IsAgent == true);

            if (leaveId.HasValue)
            {
                var leave = await _db.Leaves.FindAsync(leaveId.Value);
                if (leave == null) return NotFound("找不到請假紀錄");

                var start = leave.Start.Date;
                var end = leave.End.Date;

                // 找出已經衝突的代理人
                var busyAgents = await _db.Leaves
                    .Where(l => l.LeaveId != leave.LeaveId &&
                                l.AgentDriverId != null &&
                                l.Status == "核准" &&
                                l.Start.Date <= end &&
                                l.End.Date >= start)
                    .Select(l => l.AgentDriverId.Value)
                    .Distinct()
                    .ToListAsync();

                q = q.Where(d => !busyAgents.Contains(d.DriverId));
            }

            var list = await q
                .OrderBy(d => d.DriverName)
                .Select(d => new { driverId = d.DriverId, driverName = d.DriverName })
                .ToListAsync();

            return Ok(list);
        }
        #endregion

        #region 查詢可用司機
        //查詢可用司機
        [HttpGet("/api/drivers-available")]
        public async Task<IActionResult> GetAvailableDrivers(
        [FromQuery] DateTime useStart,
        [FromQuery] DateTime useEnd)
        {
            if (useStart == default || useEnd == default || useEnd <= useStart)
                return BadRequest(ApiResponse.Fail<object>("時間區間不正確"));

            var list = await _driverService.GetAvailableDriversAsync(useStart, useEnd);

            return Ok(ApiResponse.Ok(list, "可用駕駛清單取得成功"));
        }
        #endregion
    }
}
