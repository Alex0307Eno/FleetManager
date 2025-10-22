using Microsoft.AspNetCore.Mvc;
using Cars.Application.Services;
using Cars.Shared.Dtos;

namespace Cars.Web.Controllers
{
    [ApiController]
    [Route("api/ai/dispatch")]
    public class AiDispatchController : ControllerBase
    {
        private readonly IAiDispatchService _svc;
        public AiDispatchController(IAiDispatchService svc) => _svc = svc;

        [HttpPost("suggest")]
        public async Task<IActionResult> Suggest([FromBody] SuggestInput input)
        {
            var list = await _svc.BuildSuggestionsAsync(input);
            return Ok(new { data = list });
        }

        [HttpPost("skip")]
        public IActionResult Skip([FromBody] SuggestDecision d)
        {
            return Ok();
        }
    }

    
}
        

