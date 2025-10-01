using Cars.Data;
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
        public IActionResult Dispatch()
        {
            return View();
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
