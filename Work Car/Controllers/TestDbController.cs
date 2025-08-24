using Cars.Data;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestDbController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TestDbController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                // 嘗試連線資料庫並讀取 Driver 資料
                var drivers = _context.Drivers.Take(5).ToList();
                return Ok(new
                {
                    message = "✅ 連線成功！",
                    count = drivers.Count,
                    sample = drivers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "❌ 資料庫連線失敗",
                    error = ex.Message,
                    stack = ex.StackTrace
                });
            }
        }
    }
}
