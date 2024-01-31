using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using migrationApi.Models;
using migrationApi.Services;

namespace migrationApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MigrationController : ControllerBase
    {
        private ServiceBusService _serviceBusService;

        public MigrationController(ServiceBusService serviceBusService)
        {
            _serviceBusService = serviceBusService;
        }

        [HttpPost(Name = "MigrateAsset")]
        public async Task<IActionResult> MigrateAsset(MigrationRequest migrationRequest)
        {
            await _serviceBusService.QueueMessage(migrationRequest);

            return Ok(migrationRequest);
        }
    }
}
