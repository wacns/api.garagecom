#region

using System.Text;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Controllers;

#endregion

namespace api.garagecom.Middlewares;

public class RequestResponseLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestResponseLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // 1) Enable buffering so we can read the body multiple times
        context.Request.EnableBuffering();

        // 2) Log *all* request info
        var reqLog = await FormatRequest(context);
        logger.LogInformation("---- Incoming Request ----\n{Log}", reqLog);

        // 3) Log HttpContext.Items if any
        if (context.Items?.Count > 0)
            foreach (var kv in context.Items)
                logger.LogInformation("HttpContext.Items[{Key}] = {@Value}", kv.Key, kv.Value);

        // 4) Swap out response body to capture it
        var originalBody = context.Response.Body;
        await using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        // 5) Call the next middleware in pipeline
        await next(context);

        // 6) Log *all* response info
        var respLog = await FormatResponse(context);
        logger.LogInformation("---- Outgoing Response ----\n{Log}", respLog);

        // 7) Copy the captured response back to the real body
        memStream.Seek(0, SeekOrigin.Begin);
        await memStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }

    private static async Task<string> FormatRequest(HttpContext ctx)
    {
        var req = ctx.Request;
        var sb = new StringBuilder();

        // --- Basic request line & URL ---
        sb.AppendLine($"{req.Method} {req.GetDisplayUrl()}");
        sb.AppendLine($"Scheme: {req.Scheme}");
        sb.AppendLine($"Host: {req.Host}");
        sb.AppendLine($"Path: {req.Path}");
        sb.AppendLine($"QueryString: {req.QueryString}");
        sb.AppendLine($"Protocol: {req.Protocol}");
        sb.AppendLine($"IsHttps: {req.IsHttps}");

        // --- Connection info ---
        sb.AppendLine($"Remote: {ctx.Connection.RemoteIpAddress}:{ctx.Connection.RemotePort}");
        sb.AppendLine($"Local:  {ctx.Connection.LocalIpAddress}:{ctx.Connection.LocalPort}");

        // --- All headers ---
        sb.AppendLine("Headers:");
        foreach (var h in req.Headers)
            sb.AppendLine($"  {h.Key}: {h.Value}");

        // --- Cookies ---
        sb.AppendLine("Cookies:");
        foreach (var c in req.Cookies)
            sb.AppendLine($"  {c.Key}: {c.Value}");

        // --- Query parameters ---
        sb.AppendLine("Query Params:");
        foreach (var q in req.Query)
            sb.AppendLine($"  {q.Key}: {q.Value}");

        // --- Form fields if present ---
        if (req.HasFormContentType)
        {
            var form = await req.ReadFormAsync();
            sb.AppendLine("Form Fields:");
            foreach (var f in form)
            {
                // turn it into an IEnumerable<string>
                IEnumerable<string> values = f.Value;
                sb.AppendLine($"  {f.Key}: {string.Join(",", values)}");
            }
        }


        // --- Route values & endpoint metadata ---
        sb.AppendLine("Route Values:");
        foreach (var rv in ctx.Request.RouteValues)
            sb.AppendLine($"  {rv.Key}: {rv.Value}");

        var ep = ctx.GetEndpoint();
        if (ep != null)
        {
            sb.AppendLine($"Endpoint DisplayName: {ep.DisplayName}");
            if (ep is RouteEndpoint re)
                sb.AppendLine($"Route Pattern: {re.RoutePattern.RawText}");
            if (ep.Metadata.GetMetadata<ControllerActionDescriptor>() is { } cad)
            {
                sb.AppendLine($"Controller: {cad.ControllerName}");
                sb.AppendLine($"Action:     {cad.ActionName}");
            }
        }

        // --- User identity & claims ---
        var user = ctx.User;
        sb.AppendLine($"User Authenticated: {user.Identity?.IsAuthenticated}");
        sb.AppendLine($"User Name:          {user.Identity?.Name}");
        sb.AppendLine($"Auth Type:          {user.Identity?.AuthenticationType}");
        if ((bool)user.Identity?.IsAuthenticated)
            foreach (var claim in user.Claims)
                sb.AppendLine($"  Claim: {claim.Type} = {claim.Value}");

        // --- Body ---
        req.Body.Seek(0, SeekOrigin.Begin);
        using (var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            req.Body.Seek(0, SeekOrigin.Begin);
            sb.AppendLine("Body:");
            sb.AppendLine(body);
        }

        return sb.ToString();
    }

    private static async Task<string> FormatResponse(HttpContext ctx)
    {
        var res = ctx.Response;
        var sb = new StringBuilder();

        sb.AppendLine($"Status Code: {res.StatusCode}");
        sb.AppendLine($"ContentType: {res.ContentType}");

        sb.AppendLine("Response Headers:");
        foreach (var h in res.Headers)
            sb.AppendLine($"  {h.Key}: {h.Value}");

        // Read the response body we captured
        res.Body.Seek(0, SeekOrigin.Begin);
        using (var reader = new StreamReader(res.Body, Encoding.UTF8, leaveOpen: true))
        {
            var text = await reader.ReadToEndAsync();
            res.Body.Seek(0, SeekOrigin.Begin);
            sb.AppendLine("Body:");
            sb.AppendLine(text);
        }

        return sb.ToString();
    }
}

public static class RequestResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestResponseLoggingMiddleware>();
    }
}