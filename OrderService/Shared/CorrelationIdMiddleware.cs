
using System.Diagnostics;

namespace Shared;

public class CorrelationIdMiddleware
{
    private const string HeaderName = "x-correlation-id";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var correlationId = ctx.Request.Headers.TryGetValue(HeaderName, out var h) && !string.IsNullOrWhiteSpace(h)
            ? h.ToString()
            : Guid.NewGuid().ToString();
        ctx.Response.Headers[HeaderName] = correlationId;

        using var activity = new Activity("HTTP " + ctx.Request.Method);
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.AddTag("correlation.id", correlationId);
        activity.Start();

        ctx.Items[HeaderName] = correlationId;
        await _next(ctx);
    }
}

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();

    public static string? GetCorrelationId(this HttpContext ctx) =>
        ctx.Items.TryGetValue("x-correlation-id", out var v) ? v?.ToString() : null;
}
