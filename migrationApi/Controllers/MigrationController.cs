using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using migrationApi.Models;
using migrationApi.Services;

namespace migrationApi.Controllers
{
    [Route("api/[controller]/[action]")]
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
            try
            {
                await _serviceBusService.QueueMessage(migrationRequest);

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.ToString());
                return BadRequest(ex.Message);
            }
        }
    }
}
