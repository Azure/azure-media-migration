using Azure.Identity;
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

        [HttpPost(Name = "ListAssets")]
        public async Task<IActionResult> ListAssets(ListAssetsRequest listAssetsRequest)
        {
            var mediaService = new AmsService(new DefaultAzureCredential(), listAssetsRequest.SubscriptionId, listAssetsRequest.ResourceGroup);
            var assets = await mediaService.GetAssets(listAssetsRequest.AzureMediaServicesAccountName);

            return Ok(assets);
        }
    }
}
