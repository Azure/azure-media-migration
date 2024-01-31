using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        [HttpGet(Name = "MigrateAsset")]
        public async Task MigrateAsset()
        {
            
        }
    }
}
