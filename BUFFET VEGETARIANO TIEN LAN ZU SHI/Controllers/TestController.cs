using Microsoft.AspNetCore.Mvc;
using TIEN_LAN_ZU_SHI.Services;

namespace TIEN_LAN_ZU_SHI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly ILogService _logService;

        public TestController(ILogService logService)
        {
            _logService = logService;
        }

        [HttpGet("ok")]
        public IActionResult Ok_() => Ok(new { mensaje = "Todo bien" });

        [HttpGet("fail")]
        public IActionResult Fail()
        {
            throw new InvalidOperationException("Falla intencional para probar el log de errores");
        }

        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs()
        {
            var logs = await _logService.GetRecentLogsAsync(20);
            return Ok(logs);
        }
    }
}