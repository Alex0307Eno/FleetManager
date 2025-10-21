using Cars.Data;
using Cars.Models;
using Cars.Web.Services;
using Cars.Shared.Dtos.Leaves;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cars.Application.Services;        

namespace Cars.ApiControllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LeavesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly LeaveService _leaveService;
        public LeavesController(ApplicationDbContext db, LeaveService leaveService) 
        { 
            _db = db; 
            _leaveService = leaveService;

        }

        

        // 單一版本：取得「目前登入者」的請假紀錄
        [HttpGet("my")]
        public async Task<IActionResult> GetMyLeaves()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!int.TryParse(userIdStr, out var userId))
                    return Unauthorized("尚未登入或 Session 遺失");
                var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserId == userId);
                if (driver == null) return Unauthorized("找不到對應的司機");

                var myLeaves = await _db.Leaves
                    .Where(l => l.DriverId == driver.DriverId)
                    .Include(l => l.Driver)
                    .Include(l => l.AgentDriver)
                    .OrderByDescending(l => l.Start)
                    .Select(l => new {
                        l.LeaveId,
                        l.LeaveType,
                        l.Start,
                        l.End,
                        l.Reason,
                        l.Status,
                        l.CreatedAt,
                        driverName = l.Driver.DriverName,
                        agentDriverName = l.AgentDriver != null ? l.AgentDriver.DriverName : null 

                    })
                    .ToListAsync();


                return Ok(myLeaves);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "伺服器錯誤", detail = ex.Message });
            }
        }

        // 建立請假申請
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] LeaveDto dto)
        {
            if (dto == null) return BadRequest("❌ 請假資料不得為空");
            if (dto.End <= dto.Start) return BadRequest("❌ 結束時間必須晚於開始時間");
            if (string.IsNullOrWhiteSpace(dto.LeaveType) || string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest("❌ 假別與原因不得為空");

            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized("尚未登入或 Session 遺失");
            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserId == userId);
            if (driver == null) return Unauthorized("找不到對應的司機");

            var leave = new Leave
            {
                DriverId = driver.DriverId,
                LeaveType = dto.LeaveType,
                Start = dto.Start,
                End = dto.End,
                Reason = dto.Reason,
                Status = "待審核",
                CreatedAt = DateTime.Now
            };

            _db.Leaves.Add(leave);
            try
            {
                var (ok,err) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err!; 
                return Ok(new { message = "✅ 請假申請成功", id = leave.LeaveId });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = "❌ 資料儲存失敗", detail = ex.InnerException?.Message ?? ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Leaves
                .Include(l => l.Driver)
                .Include(l => l.AgentDriver)
                .OrderByDescending(l => l.Start)
                .Select(l => new {
                    l.LeaveId,
                    l.LeaveType,
                    l.Start,
                    l.End,
                    l.Reason,
                    l.Status,
                    l.CreatedAt,
                    driverName = l.Driver.DriverName,
                    agentDriverName = l.AgentDriver != null ? l.AgentDriver.DriverName : null 

                })
                .ToListAsync();

            return Ok(list);
        }
        #region 請假狀態審核
        // 更新請假狀態（核准或駁回）
        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
        {

            try
            {
                var leave = await _db.Leaves
                    .Include(l => l.Driver)
                    .FirstOrDefaultAsync(l => l.LeaveId == id);

                if (leave == null) return NotFound("找不到請假紀錄");

                var status = dto.Status?.Trim();
                if (status != "核准" && status != "駁回")
                    return BadRequest("狀態無效");

                leave.Status = status;
                string agentName = null;

                if (status == "核准")
                {
                    if (!dto.AgentDriverId.HasValue)
                        return BadRequest("核准請假時必須指定代理人");
                    if (dto.AgentDriverId.Value == leave.DriverId)
                        return BadRequest("代理人不可與請假人相同");

                    var agent = await _db.Drivers.FindAsync(dto.AgentDriverId.Value);
                    if (agent == null) return BadRequest("代理人不存在");

                    var start = leave.Start.Date;
                    var end = leave.End.Date;

                    // ⚠️ 檢查代理人是否已被指派在同一時段
                    bool alreadyAssigned = await _db.Leaves.AnyAsync(l =>
                        l.LeaveId != leave.LeaveId &&
                        l.AgentDriverId == dto.AgentDriverId.Value &&
                        l.Status == "核准" &&
                        l.Start.Date <= end && l.End.Date >= start
                    );

                    if (alreadyAssigned)
                        return BadRequest("該代理人在請假期間已有其他指派，請選擇其他代理人");

                    leave.AgentDriverId = dto.AgentDriverId.Value;
                    agentName = agent.DriverName;

                    // === 更新班表 ===
                    var schedules = await _db.Schedules
                        .Where(s => s.DriverId == leave.DriverId &&
                                    s.WorkDate >= leave.Start.Date &&
                                    s.WorkDate <= leave.End.Date)
                        .ToListAsync();

                    foreach (var s in schedules)
                        s.DriverId = dto.AgentDriverId.Value;

                    // === 新增 DriverDelegation ===
                    var deleg = new DriverDelegation
                    {
                        PrincipalDriverId = leave.DriverId,
                        AgentDriverId = dto.AgentDriverId.Value,
                        StartDate = leave.Start.Date,
                        EndDate = leave.End.Date,
                        Reason = "請假",
                        CreatedAt = DateTime.Now
                    };
                    _db.DriverDelegations.Add(deleg);
                }


                var (ok, err) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err!;
                //查詢受影響行程
                var affected = await _leaveService.GetAffectedDispatchesAsync(
                                leave.DriverId,
                                leave.Start,
                                leave.End
                                );

                // 然後照舊做投影
                var affectedList = affected.Select(x => new
                {
                    x.DispatchId,
                    Start = x.CarApplication.UseStart,
                    End = x.CarApplication.UseEnd,
                    x.CarApplication.Origin,
                    x.CarApplication.Destination,
                    x.DispatchStatus
                }).ToList();


                if (status == "核准")
                {
                    // 呼叫通知服務
                    var leaveService = HttpContext.RequestServices.GetRequiredService<LeaveService>();
                    await leaveService.HandleDriverLeaveAsync(leave.LeaveId);
                }
                var msg = status == "核准"
                         ? $"指派代理人 {agentName}。駕駛 {leave.Driver.DriverName} 請假期間有 {affectedList.Count} 筆行程受影響。"
                         : $"請假紀錄 {id} 已駁回";

                return Ok(new { message = msg, affected = affectedList });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
        }
        // 檢查請假期間受影響的派車行程
        [HttpGet("check-affected")]
        public async Task<IActionResult> CheckAffected(int driverId, DateTime start, DateTime end)
        {
            if (driverId <= 0 || start == default || end == default)
                return BadRequest("參數錯誤");

            // 撈出請假期間的派車
            var affected = await _db.Dispatches
                .Include(x => x.CarApplication)
                .Where(x =>
                    x.DriverId == driverId &&
                    x.CarApplication.UseStart <= end &&
                    x.CarApplication.UseEnd >= start &&
                    (x.DispatchStatus == "已派車"
                     || x.DispatchStatus == "進行中"
                     || x.DispatchStatus == "待出勤"))
                .Select(x => new
                {
                    x.DispatchId,
                    Start = x.CarApplication.UseStart,
                    End = x.CarApplication.UseEnd,
                    x.CarApplication.Origin,
                    x.CarApplication.Destination,
                    x.DispatchStatus
                })
                .OrderBy(x => x.Start)
                .ToListAsync();

            return Ok(affected);
        }



        // 指派代理人（僅限已核准的請假）
        [HttpPost("{id}/agent")]
        public async Task<IActionResult> AssignAgent(int id, [FromBody] int agentDriverId)
        {
            var leave = await _db.Leaves.FindAsync(id);
            if (leave == null) return NotFound("找不到請假紀錄");

            if (leave.Status != "核准")
                return BadRequest("只有核准的請假紀錄才能指派代理人");

            var agent = await _db.Drivers.FirstOrDefaultAsync(d => d.DriverId == agentDriverId && d.IsAgent == true);
            if (agent == null) return BadRequest("代理人不存在或不可被指派");

            leave.AgentDriverId = agentDriverId;
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            return Ok(new { message = $"代理人已指派為 {agent.DriverName}" });
        }
        #endregion

    }
}
