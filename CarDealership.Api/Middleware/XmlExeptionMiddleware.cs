namespace CarDealershipApi.Middleware;

public class XmlExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<XmlExceptionMiddleware> _logger;

    public XmlExceptionMiddleware(RequestDelegate next, ILogger<XmlExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request {Path}", context.Request.Path);
            await HandleExceptionAsync(context);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context)
    {
        context.Response.ContentType = "application/xml";
        context.Response.StatusCode = 500;

        // Generic message, stack trace hidden
        var xmlError = "<error><code>500</code><message>An internal server error occurred.</message></error>";
        return context.Response.WriteAsync(xmlError);
    }
}