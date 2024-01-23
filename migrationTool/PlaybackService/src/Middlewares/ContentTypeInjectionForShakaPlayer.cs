using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace PlaybackService;

public class ContentTypeInjectionForShakaPlayer
{
    private readonly RequestDelegate _next;

    public ContentTypeInjectionForShakaPlayer(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrEmpty(context.Request.ContentType))
        {
            if (context.Request.Path == "/.clearkeys")
            {
                context.Request.ContentType = "application/json";
            }
        }

        return _next(context);
    }
}
