// Controllers/FavoriteLocationsController.cs
using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FavoriteLocationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public FavoriteLocationsController(ApplicationDbContext db)
        {
            _db = db;
        }
        #region 我的最愛
        private int GetUserId()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int val;
            return int.TryParse(uid, out val) ? val : 0;
        }

        public class FavoriteDto
        {
            public string CustomName { get; set; }
            public string Address { get; set; }
            public string PlaceId { get; set; }
            public double? Lat { get; set; }
            public double? Lng { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetMy()
        {
            var uid = GetUserId();
            var list = await _db.FavoriteLocations
                .AsNoTracking()
                .Where(x => x.UserId == uid)
                .OrderBy(x => x.CustomName)
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] FavoriteDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.CustomName) || string.IsNullOrWhiteSpace(dto.Address))
                return BadRequest(new { message = "請輸入自訂名稱與地址" });

            var uid = GetUserId();

            // 檢查同使用者是否已有同名別名
            bool nameExists = await _db.FavoriteLocations
                .AnyAsync(x => x.UserId == uid && x.CustomName == dto.CustomName);

            if (nameExists)
                return Conflict(new { message = "此別名已存在，請換一個名稱" });

            var now = DateTime.UtcNow;

            var entity = new FavoriteLocation
            {
                UserId = uid,
                CustomName = dto.CustomName.Trim(),
                Address = dto.Address.Trim(),
                PlaceId = string.IsNullOrWhiteSpace(dto.PlaceId) ? null : dto.PlaceId.Trim(),
                Lat = dto.Lat,
                Lng = dto.Lng,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.FavoriteLocations.Add(entity);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 資料被別人改過 → 可以提示用戶重試
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // 一般資料庫錯誤
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                // 500 錯誤
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
            return Ok(entity);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] FavoriteDto dto)
        {
            var uid = GetUserId();
            var entity = await _db.FavoriteLocations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
            if (entity == null) return NotFound(new { message = "資料不存在" });

            if (!string.IsNullOrWhiteSpace(dto.CustomName) && dto.CustomName != entity.CustomName)
            {
                bool nameExists = await _db.FavoriteLocations
                    .AnyAsync(x => x.UserId == uid && x.CustomName == dto.CustomName && x.Id != id);
                if (nameExists) return Conflict(new { message = "此別名已存在" });
                entity.CustomName = dto.CustomName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.Address)) entity.Address = dto.Address.Trim();
            entity.PlaceId = string.IsNullOrWhiteSpace(dto.PlaceId) ? entity.PlaceId : dto.PlaceId.Trim();
            entity.Lat = dto.Lat ?? entity.Lat;
            entity.Lng = dto.Lng ?? entity.Lng;
            entity.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 資料被別人改過 → 可以提示用戶重試
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // 一般資料庫錯誤
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                // 500 錯誤
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
            return Ok(entity);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = GetUserId();
            var entity = await _db.FavoriteLocations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
            if (entity == null) return NotFound(new { message = "資料不存在" });

            _db.FavoriteLocations.Remove(entity);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 資料被別人改過 → 可以提示用戶重試
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // 一般資料庫錯誤
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                // 500 錯誤
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
            return Ok(new { message = "已刪除" });
        }
        #endregion 
    }
}
