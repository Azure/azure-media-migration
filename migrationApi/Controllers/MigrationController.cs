using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using migrationApi.Models;
using migrationApi.Services;
using System.Text.Json;

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
        public async Task<IActionResult> ListAssets(MigrationRequest migrationRequest)
        {
            var mediaService = new AmsService(new DefaultAzureCredential(), migrationRequest.SubscriptionId, migrationRequest.ResourceGroup);
            var assets = await mediaService.GetAssets(migrationRequest.AzureMediaServicesAccountName);

            var asset = assets.Where(asset => asset.Data.Name == migrationRequest.AssetName).FirstOrDefault();

            if (asset != null)
            {
                var message = new MigrationMessage()
                {
                    SubscriptionId = migrationRequest.SubscriptionId,
                    ResourceGroup = migrationRequest.ResourceGroup,
                    SourceStorageAccountName = migrationRequest.SourceStorageAccountName,
                    TargetStorageAccountName = migrationRequest.TargetStorageAccountName,
                    AssetName = asset.Data.Container
                };

                await _serviceBusService.QueueMessage(JsonSerializer.Serialize(message));

                return Ok(asset);
            }
            else
            {
                return NotFound();
            }
        }
    }
}
