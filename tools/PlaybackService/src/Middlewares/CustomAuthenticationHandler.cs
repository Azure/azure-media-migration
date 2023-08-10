using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace PlaybackService;

public class CustomAuthenticationHandler : AuthenticationHandler<CustomAuthenticationHandlerOptions>
{
    public CustomAuthenticationHandler(
        IOptionsMonitor<CustomAuthenticationHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
    }

    public static string SchemeName => "Custom";

    /// <summary>
    /// You can customize your authentication logic here.
    /// For demo purpose, will authenticate all requests if the Authentication header is not empty.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Get the token from uri
        var token = Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("No token is found."));
        }

        // TODO: Authenticate the token.
        var newToken = token;

        // Put the new token into the ticket.
        var claimsIdentity = new ClaimsIdentity(SchemeName);
        claimsIdentity.AddClaim(new Claim("token", newToken));

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
