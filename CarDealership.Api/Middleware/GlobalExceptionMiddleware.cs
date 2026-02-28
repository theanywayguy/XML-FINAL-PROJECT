using System.Xml.Serialization;
using CarDealershipApi.Models; // Make sure this points to where your XmlError class lives

namespace CarDealershipApi.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Let the request proceed to the controllers
            await _next(context);
        }
        catch (Exception ex)
        {
            // If anything explodes, catch it here globally
            _logger.LogError(ex, "GlobalErrorHandler: An unhandled exception occurred while processing the request.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Force the response to be XML and 500 Internal Server Error
        context.Response.ContentType = "application/xml";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var errorResponse = new XmlError
        {
            Code = 500,
            Message = "An unexpected internal server error occurred."
            // Note: In development, you could map exception.Message here, 
            // but in production, it's safer to hide stack traces/raw errors.
        };

        // Serialize our XmlError object directly into the response stream
        var serializer = new XmlSerializer(typeof(XmlError));

        using var stringWriter = new StringWriter();
        serializer.Serialize(stringWriter, errorResponse);

        return context.Response.WriteAsync(stringWriter.ToString());
    }
}