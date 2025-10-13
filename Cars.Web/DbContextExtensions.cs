using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Web.Services
{
    public static class DbContextExtensions
    {
        public static async Task<(bool ok, IActionResult? result)>
            TrySaveChangesAsync(this DbContext db, ControllerBase controller)
        {
            try
            {
                await db.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return (false, controller.Conflict(new
                {
                    message = "資料已被更新，請重新整理後再試。",
                    detail = ex.Message
                }));
            }
            catch (DbUpdateException ex)
            {
                return (false, controller.BadRequest(new
                {
                    message = "資料儲存失敗，請確認輸入是否正確。",
                    detail = ex.InnerException?.Message ?? ex.Message
                }));
            }
            catch (Exception ex)
            {
                return (false, controller.StatusCode(500, new
                {
                    message = "伺服器內部錯誤",
                    error = ex.Message
                }));
            }
        }
    }


}
