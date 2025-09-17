using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Agent
{

    public class AgentsController : Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult _AgentForm() 
        {
            return PartialView();
        }

        public IActionResult Create()
        {

            return View();
        }
        public IActionResult Edit(int id)
        {
           
            return View();
        }
    }
}
