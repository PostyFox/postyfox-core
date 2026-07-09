using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PostyFox.Web.Extensions;

public static class SecurityExtensions
{
    /// <summary>
    /// Global fixed-window rate limiter, partitioned by authenticated user (falling back to client
    /// IP). Config: <c>RateLimit:PermitsPerWindow</c> (default 300), <c>RateLimit:WindowSeconds</c>
    /// (default 60). Rejected requests get HTTP 429.
    /// </summary>
    public static IServiceCollection AddPostyFoxRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                // Read at request time so the final (possibly test-injected) configuration is honoured.
                var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
                var permits = config.GetValue<int?>("RateLimit:PermitsPerWindow") ?? 300;
                var windowSeconds = config.GetValue<int?>("RateLimit:WindowSeconds") ?? 60;

                var key = ctx.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? ctx.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0
                });
            });
        });
        return services;
    }

    /// <summary>Adds conservative security response headers to every response.</summary>
    public static IApplicationBuilder UsePostyFoxSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            await next();
        });
}
