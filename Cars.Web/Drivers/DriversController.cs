using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Controllers  
{
    [Authorize] 
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
            return PartialView(); 
        }

        // 建立駕駛
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 編輯駕駛
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
