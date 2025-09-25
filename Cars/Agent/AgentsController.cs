using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Controllers
{
    [Authorize]
    [Route("agents")]
    public class AgentsController : Controller
    {
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("form")]
        public IActionResult _AgentForm()
        {
            return PartialView("_AgentForm");
        }

        [HttpGet("create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Driver model)
        {
            if (!ModelState.IsValid) return View(model);

            // 儲存資料...
            return RedirectToAction("Index");
        }

        [HttpGet("edit/{id:int}")]
        public IActionResult Edit(int id)
        {
            // 讀取資料...
            return View();
        }

        [HttpPost("edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Driver model)
        {
            if (!ModelState.IsValid) return View(model);

            // 更新資料...
            return RedirectToAction("Index");
        }
    }
}
