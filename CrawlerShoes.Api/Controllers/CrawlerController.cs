using Microsoft.AspNetCore.Mvc;
using CrawlerShoes.Api.Services.Interfaces;

namespace CrawlerShoes.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CrawlerController : ControllerBase
    {
        private readonly INetshoesCrawlerService _crawlerService;

        public CrawlerController(INetshoesCrawlerService crawlerService)
        {
            _crawlerService = crawlerService;
        }

        [HttpGet("netshoes")]
        public async Task<IActionResult> GetShoes([FromQuery] int page = 1)
        {
            var result = await _crawlerService.GetShoesAsync(page);
            return Ok(result);
        }
    }
}
