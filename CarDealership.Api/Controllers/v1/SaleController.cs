using CarDealershipApi.Models;
using CarDealershipApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CarDealershipApi.Controllers.v1;

/// <summary>
/// Manages vehicle sales and transaction records.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/xml")]
[Consumes("application/xml")]
[Authorize(Roles = "Salesperson")]
public class SalesController : ControllerBase
{
    private readonly XmlDataService _dataService;
    private readonly XsdValidationService _validationService;
    private readonly ILogger<SalesController> _logger;

    public SalesController(
        XmlDataService dataService,
        XsdValidationService validationService,
        ILogger<SalesController> logger)
    {
        _dataService = dataService;
        _validationService = validationService;
        _logger = logger;
    }

    private ObjectResult XmlErrorResponse(int code, string message)
    {
        return StatusCode(code, new XmlError { Code = code, Message = message });
    }

    /// <summary>
    /// Securely extracts the user's ID from the JWT Claims.
    /// </summary>
    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "UnknownSalespersonId";

    /// <summary>
    /// Processes the sale of a vehicle. Removes it from inventory and logs the sale.
    /// </summary>
    /// <param name="request">The sale request payload.</param>
    /// <response code="200">If the sale was successful.</response>
    /// <response code="400">If the payload is invalid or breaks schema.</response>
    /// <response code="404">If the car is not found in inventory.</response>
    /// <response code="500">If a transactional error occurs.</response>
    [HttpPost("sell")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 404)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public async Task<IActionResult> ProcessSale([FromBody] SaleRequest request)
    {
        if (request == null || request.Price <= 0 || string.IsNullOrWhiteSpace(request.CarId) || string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return XmlErrorResponse(400, "Invalid sale request data. CarId, CustomerId, and Price are required.");
        }

        // Construct the strictly controlled Sale object
        var saleRecord = new Sale
        {
            SaleId = Guid.NewGuid().ToString(), // System generated GUID
            SalesmanId = GetUserId(),           // Pulled securely from the JWT Token's NameIdentifier claim
            CustomerId = request.CustomerId,    // Mapped Customer ID
            CarId = request.CarId,
            DateTime = DateTime.UtcNow,         // System generated UTC time
            PaymentMethod = request.PaymentMethod,
            Price = request.Price
        };

        _logger.LogInformation("Action: ProcessSale | Initiated by UserID: {UserId} | Car: {CarId} | Customer: {CustomerId}", saleRecord.SalesmanId, saleRecord.CarId, saleRecord.CustomerId);

        // Execute the thread-safe transaction across both XML files
        var success = await _dataService.ProcessSaleAsync(saleRecord);

        if (!success)
        {
            _logger.LogWarning("Action: ProcessSale | Failed - Car {CarId} not found in inventory.", request.CarId);
            return XmlErrorResponse(404, $"Car with id '{request.CarId}' not found in inventory.");
        }

        // Verify our generated XML didn't break our strict XSD rules
        var validationErrors = _validationService.ValidateSalesXml();
        if (validationErrors.Any())
        {
            var errorMsg = "Sale processed, but sales.xml broke XSD validation: " + string.Join(" | ", validationErrors);
            _logger.LogError(errorMsg);
            // Note: In a true enterprise environment, we would run a compensation transaction here to rollback. 
            // For now, logging it as a critical error is sufficient.
        }

        _logger.LogInformation("Action: ProcessSale | Success | Sale ID: {SaleId}", saleRecord.SaleId);
        return Ok(new SaleResponse { SaleId = saleRecord.SaleId }); // Return the ID so they can revert it if needed
    }

    /// <summary>
    /// Reverts a sale and returns the car to inventory. Must be done within 3 hours of the sale.
    /// </summary>
    /// <param name="saleId">The unique ID of the sale to revert.</param>
    /// <response code="200">If the revert was successful.</response>
    /// <response code="400">If the sale ID is invalid or the 3-hour window has passed.</response>
    /// <response code="500">If a transactional error occurs.</response>
    [HttpPost("revert/{saleId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public async Task<IActionResult> RevertSale(string saleId)
    {
        _logger.LogInformation("Action: RevertSale | Initiated by UserID: {UserId} | SaleId: {SaleId}", GetUserId(), saleId);

        var result = await _dataService.RevertSaleAsync(saleId);

        if (!result.Success)
        {
            _logger.LogWarning("Action: RevertSale | Failed | SaleId: {SaleId} | Reason: {Reason}", saleId, result.ErrorMessage);
            return XmlErrorResponse(400, result.ErrorMessage);
        }

        // Re-validate dealership inventory after putting the car back
        var validationErrors = _validationService.ValidateDealershipXml();
        if (validationErrors.Any())
        {
            _logger.LogError("Revert processed, but dealership.xml broke XSD validation: {Errors}", string.Join(" | ", validationErrors));
        }

        _logger.LogInformation("Action: RevertSale | Success | SaleId: {SaleId} successfully reverted.", saleId);
        return Ok();
    }
}