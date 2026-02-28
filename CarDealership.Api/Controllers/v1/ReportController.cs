using CarDealershipApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarDealershipApi.Controllers.v1;

/// <summary>
/// Manages dealership reporting and analytics.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Manager")] // Restrict entire controller to Managers
public class ReportsController : ControllerBase
{
    private readonly XsltTransformationService _xsltService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        XsltTransformationService xsltService,
        ILogger<ReportsController> logger)
    {
        _xsltService = xsltService;
        _logger = logger;
    }

    private string GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
    private string GetUsername() => User.Identity?.Name ?? "Unknown";

    /// <summary>
    /// Generates an HTML report of the XML inventory using an XSLT transformation.
    /// </summary>
    /// <response code="200">Returns the formatted HTML report.</response>
    /// <response code="500">If the XSLT transformation fails.</response>
    [HttpGet("/report")]
    [Produces("text/html")]
    public IActionResult GetInventoryReport()
    {
        _logger.LogInformation("Action: GetInventoryReport | IP: {IpAddress} | Status: Success | Manager: {Username} generated HTML report.", GetIpAddress(), GetUsername());

        var html = _xsltService.GenerateHtmlReport();

        return Content(html, "text/html");
    }
}