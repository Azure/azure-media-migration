using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PlaybackService;

public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger; ;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var logger = _logger;

        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            var err = Encoding.UTF8.GetBytes(ex.Message);
            await context.Response.Body.WriteAsync(err, 0, err.Length);
        }
        catch (RequestFailedException ex)
        {
            context.Response.StatusCode = ex.Status;
            var err = Encoding.UTF8.GetBytes(ex.Message);
            await context.Response.Body.WriteAsync(err, 0, err.Length);
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            var err = Encoding.UTF8.GetBytes(ex.Message);
            await context.Response.Body.WriteAsync(err, 0, err.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception, will return 500 error code to user.");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
    }
}
