namespace BioShieldLens.Middleware;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    // Public paths that don't require authentication
    private readonly string[] _publicPaths = {
        "/auth/login",
        "/auth/accessdenied",
        "/css/",
        "/js/",
        "/lib/",
        "/favicon.ico"
    };

    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Check if authentication is enabled
        var authEnabled = configuration.GetValue<bool>("Auth:Enabled", true);
        if (!authEnabled)
        {
            await _next(context);
            return;
        }

        // Allow public paths
        if (_publicPaths.Any(p => path.StartsWith(p)))
        {
            await _next(context);
            return;
        }

        // Check if user is authenticated
        var userEmail = context.Session.GetString("UserEmail");
        if (string.IsNullOrEmpty(userEmail))
        {
            // Store the original URL to redirect back after login
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"/Auth/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        await _next(context);
    }
}

public static class AuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthenticationMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthenticationMiddleware>();
    }
}


