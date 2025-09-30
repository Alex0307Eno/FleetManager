using Cars.Data;
using Cars.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Dispatch
{

    public class DispatchesController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DispatchesController(ApplicationDbContext db) { _db = db; }

        [Authorize]
        public async Task<IActionResult> Dispatch(int page = 1, int pageSize = 10)
        {
            var query = _db.CarApplications.AsNoTracking();

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(c => c.UseStart)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Pagination = new PaginationModel
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = total,
                Action = nameof(Dispatch),   // ← 分頁連到同一頁
                Controller = "Dispatches"
            };

            return View(items); // ← 把這一頁的資料丟給 View
        }
        [Authorize]
        public IActionResult CarApply()
        {
            return View();
        }
        [Authorize]
        public IActionResult Record()
        {
            return View();
        }
       


    }
}
