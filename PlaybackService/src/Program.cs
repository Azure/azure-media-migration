using PlaybackService;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;

// Create application builder.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();
builder.Services.AddControllers();
builder.Services.AddSingleton<PlaybackController.DefaultStorageCredential>();
builder.Services.AddSingleton<IBlobClientFactory, AzureServiceFactory>();
builder.Services.AddSingleton<SecretClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PlaybackServiceOptions>>();
    if (string.IsNullOrEmpty(options.Value.AzureKeyVaultAccountName))
    {
        throw new ArgumentNullException("AzureKeyVaultAccountName can't be found in options.");
    }
    return new SecretClient(new Uri($"https://{options.Value.AzureKeyVaultAccountName}.vault.azure.net"), new DefaultAzureCredential());
});
builder.Services.AddSingleton<KeyCache>();

// Configure options.
builder.Services.Configure<PlaybackServiceOptions>(builder.Configuration.GetSection("PlaybackService"));
builder.Services.AddOptions<PlaybackController.Options>()
    .Configure<IOptionsSnapshot<PlaybackServiceOptions>>((o, op) =>
{
    o.AllowedFileExtensions = op.Value.AllowedFileExtensions.Split(' ').Where(k => !string.IsNullOrEmpty(k)).ToArray();
});

// Configure authentication.
builder.Services
    .AddAuthentication(CustomAuthenticationHandler.SchemeName)
    .AddScheme<CustomAuthenticationHandlerOptions, CustomAuthenticationHandler>(CustomAuthenticationHandler.SchemeName, configureOptions: null);

// Build application.
var app = builder.Build();
app.UseMiddleware<GlobalExceptionHandler>();
app.UseMiddleware<ContentTypeInjectionForShakaPlayer>();

var appOptions = app.Services.GetRequiredService<IOptions<PlaybackServiceOptions>>();
if (appOptions.Value.EnableDebugUI)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseStaticFiles(new StaticFileOptions()
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"))
    });
}

app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Run.
await app.RunAsync();

internal class PlaybackServiceOptions
{
    public required string AzureKeyVaultAccountName { get; set; }

    public string AllowedFileExtensions { get; set; } = ".mpd .m3u8 .m3u .mp4 .vtt";

    public bool EnableDebugUI { get; set; } = false;
}