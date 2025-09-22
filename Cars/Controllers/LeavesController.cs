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

        // ✅ 單一版本：取得「目前登入者」的請假紀錄
        [HttpGet("my")]
        public async Task<IActionResult> GetMyLeaves()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!int.TryParse(userIdStr, out var userId))
                    return Unauthorized("尚未登入或 Session 遺失");

                var myLeaves = await _db.Leaves
                    .Where(l => l.UserId == userId)
                    .OrderByDescending(l => l.Start)
                    .ToListAsync();

                return Ok(myLeaves);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "伺服器錯誤", detail = ex.Message });
            }
        }

        // 建立請假申請（保持不變）
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

            var leave = new Leave
            {
                UserId = userId,
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

        // GET: api/Leaves
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Leaves
                .OrderByDescending(l => l.Start)
                .ToListAsync();

            return Ok(list);
        }


        // POST: api/Leaves/{id}/status
        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] dynamic body)
        {
            var leave = await _db.Leaves.FindAsync(id);
            if (leave == null) return NotFound("找不到請假紀錄");

            string status = (string)body?.status;
            if (status != "核准" && status != "駁回") return BadRequest("狀態無效");

            leave.Status = status;
            await _db.SaveChangesAsync();
            return Ok(new { message = $"請假紀錄 {id} 已更新為 {status}" });
        }

    }
}
