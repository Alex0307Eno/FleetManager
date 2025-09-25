using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Controllers  // ✅ 建議放在 Controllers 命名空間
{
    [Authorize] // ✅ 可以直接加在 Controller 層級，全部 Action 都會驗證
    public class DriversController : Controller
    {
        // 駕駛排班
        public IActionResult Schedule()
        {
            return View();
        }

        // 駕駛列表
        public IActionResult Index()
        {
            return View();
        }

        // 駕駛表單（部分檢視）
        public IActionResult _DriverForm()
        {
            return PartialView(); // ✅ 改成 PartialView
        }

        // 建立駕駛（GET: /Drivers/Create）
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 編輯駕駛（GET: /Drivers/Edit/5）
        [HttpGet]
        public IActionResult Edit(int id)
        {
            
            return View();
        }

        // 請假申請
        public IActionResult LeaveApply()
        {
            return View();
        }

        // 請假審核
        public IActionResult LeaveReview()
        {
            return View();
        }
    }
}
