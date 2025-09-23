using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq; 
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LeavesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public LeavesController(ApplicationDbContext db) { _db = db; }

        public class LeaveDto
        {
            public string LeaveType { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string Reason { get; set; }
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
                await _db.SaveChangesAsync();
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



      

        public class UpdateStatusDto
        {
            public string Status { get; set; }
            public int? AgentDriverId { get; set; } 
        }

        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
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


            if (status == "核准" && dto.AgentDriverId.HasValue)
            {
                if (dto.AgentDriverId.Value == leave.DriverId)
                    return BadRequest("代理人不可與請假人相同");

                var agent = await _db.Drivers.FindAsync(dto.AgentDriverId.Value);
                if (agent == null) return BadRequest("代理人不存在");

                var start = leave.Start.Date;
                var end = leave.End.Date;

                // ⚠️ 檢查整段日期區間是否已有其他核准的請假指派給同一代理人
                bool alreadyAssigned = await _db.Leaves.AnyAsync(l =>
                    l.LeaveId != leave.LeaveId &&               // 排除自己
                    l.AgentDriverId == dto.AgentDriverId.Value &&
                    l.Status == "核准" &&
                    (
                        // 時間區間有重疊：另一筆 Start <= 本次 End，另一筆 End >= 本次 Start
                        l.Start.Date <= end && l.End.Date >= start
                    )
                );

                if (alreadyAssigned)
                    return BadRequest("該代理人在請假期間已有其他指派，請選擇其他代理人");

                leave.AgentDriverId = dto.AgentDriverId.Value;
                agentName = agent.DriverName;

            }
            if (status == "核准" && dto.AgentDriverId.HasValue)
            {
                // 找出該司機請假區間內的班表
                var schedules = await _db.Schedules
                    .Where(s => s.DriverId == leave.DriverId &&
                                s.WorkDate >= leave.Start.Date &&
                                s.WorkDate <= leave.End.Date)
                    .ToListAsync();

                foreach (var s in schedules)
                {
                    // 改成代理人上班
                    s.DriverId = dto.AgentDriverId.Value;
                }
            }
            // 如果是核准，新增 DriverDelegation 紀錄
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

            await _db.SaveChangesAsync();
            var msg = agentName == null
                    ? $"請假紀錄 {id} 已更新為 {status}"
                    : $"請假紀錄 {id} 已更新為 {status}，並指派代理人 {agentName}";

            return Ok(new { message = msg });
        }





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
            await _db.SaveChangesAsync();

            return Ok(new { message = $"代理人已指派為 {agent.DriverName}" });
        }

    }
}
