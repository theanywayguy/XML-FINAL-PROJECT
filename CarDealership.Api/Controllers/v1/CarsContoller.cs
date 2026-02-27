using CarDealershipApi.Models;
using CarDealershipApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarDealershipApi.Controllers.v1;

/// <summary>
/// Manages the dealership's vehicle inventory.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/xml")]
[Consumes("application/xml")]
[Authorize(Roles = "Manager,Salesperson")] // Base access requires at least Salesperson role
public class CarsController : ControllerBase
{
    private readonly XmlDataService _dataService;
    private readonly XsdValidationService _validationService;
    private readonly XsltTransformationService _xsltService;
    private readonly VinIntegrationService _vinService;
    private readonly ILogger<CarsController> _logger;

    public CarsController(
        XmlDataService dataService,
        XsdValidationService validationService,
        XsltTransformationService xsltService,
        VinIntegrationService vinService,
        ILogger<CarsController> logger)
    {
        _dataService = dataService;
        _validationService = validationService;
        _xsltService = xsltService;
        _vinService = vinService;
        _logger = logger;
    }

    private ObjectResult XmlErrorResponse(int code, string message)
    {
        return StatusCode(code, new XmlError { Code = code, Message = message });
    }

    private string GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
    private string GetUsername() => User.Identity?.Name ?? "Unknown";

    /// <summary>
    /// Retrieves all cars in the inventory.
    /// </summary>
    /// <response code="200">Returns the list of cars.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<Car>), 200)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public IActionResult GetCars()
    {
        try
        {
            var cars = _dataService.GetAllCars().ToList();
            _logger.LogInformation("Action: GetCars | IP: {IpAddress} | Status: Success | User: {Username} retrieved all cars.", GetIpAddress(), GetUsername());
            return Ok(cars);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action: GetCars | IP: {IpAddress} | Status: Failed - Internal error for user {Username}.", GetIpAddress(), GetUsername());
            return XmlErrorResponse(500, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// Retrieves a specific car by its ID (VIN).
    /// </summary>
    /// <param name="id">The unique ID (VIN) of the car.</param>
    /// <response code="200">Returns the requested car.</response>
    /// <response code="404">If the car is not found.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Car), 200)]
    [ProducesResponseType(typeof(XmlError), 404)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public IActionResult GetCar(string id)
    {
        try
        {
            var car = _dataService.GetCarById(id);
            if (car == null)
            {
                _logger.LogWarning("Action: GetCar | IP: {IpAddress} | Status: Failed - Car {CarId} not found. | User: {Username}", GetIpAddress(), id, GetUsername());
                return XmlErrorResponse(404, $"Car with id {id} not found.");
            }

            _logger.LogInformation("Action: GetCar | IP: {IpAddress} | Status: Success - Retrieved car {CarId}. | User: {Username}", GetIpAddress(), id, GetUsername());
            return Ok(car);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action: GetCar | IP: {IpAddress} | Status: Failed - Error retrieving car {CarId}. | User: {Username}", GetIpAddress(), id, GetUsername());
            return XmlErrorResponse(500, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// Adds a new car to the inventory and strictly validates it against the XML Schema.
    /// </summary>
    /// <param name="car">The car XML payload.</param>
    /// <response code="201">Returns the newly created car.</response>
    /// <response code="400">If the XML payload is invalid or breaks XSD rules.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Car), 201)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public IActionResult AddCar([FromBody] Car car)
    {
        try
        {
            if (car == null)
            {
                _logger.LogWarning("Action: AddCar | IP: {IpAddress} | Status: Failed - Invalid XML payload. | User: {Username}", GetIpAddress(), GetUsername());
                return XmlErrorResponse(400, "Invalid XML payload.");
            }

            _dataService.AddCar(car);

            // Validate the entire document after insertion to ensure academic strictness
            var validationErrors = _validationService.ValidateDealershipXml();
            if (validationErrors.Any())
            {
                // Rollback (delete) since it broke schema
                _dataService.DeleteCar(car.Id);

                var errorMsg = "Added car breaks XSD validation: " + string.Join(" | ", validationErrors);
                _logger.LogWarning("Action: AddCar | IP: {IpAddress} | Status: Failed - XSD validation broke. | User: {Username} | Errors: {Errors}", GetIpAddress(), GetUsername(), errorMsg);

                return XmlErrorResponse(400, errorMsg);
            }

            _logger.LogInformation("Action: AddCar | IP: {IpAddress} | Status: Success - Created car {CarId}. | User: {Username}", GetIpAddress(), car.Id, GetUsername());
            return CreatedAtAction(nameof(GetCar), new { id = car.Id }, car);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Action: AddCar | IP: {IpAddress} | Status: Failed - Argument error. | User: {Username} | Message: {Message}", GetIpAddress(), GetUsername(), ex.Message);
            return XmlErrorResponse(400, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action: AddCar | IP: {IpAddress} | Status: Failed - Unexpected error. | User: {Username}", GetIpAddress(), GetUsername());
            return XmlErrorResponse(500, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// Updates the price of an existing car in the inventory.
    /// </summary>
    /// <param name="id">The unique ID (VIN) of the car.</param>
    /// <param name="update">The new price value payload.</param>
    /// <response code="200">If the update was successful.</response>
    /// <response code="400">If the price payload is invalid or breaks XSD rules.</response>
    /// <response code="404">If the car is not found.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 404)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public IActionResult UpdateCarPrice(string id, [FromBody] PriceUpdate update)
    {
        try
        {
            if (update == null || update.NewPrice <= 0)
            {
                _logger.LogWarning("Action: UpdatePrice | IP: {IpAddress} | Status: Failed - Invalid price data. | Car: {CarId} | User: {Username}", GetIpAddress(), id, GetUsername());
                return XmlErrorResponse(400, "Invalid price data in XML.");
            }

            var success = _dataService.UpdateCarPrice(id, update.NewPrice);
            if (!success)
            {
                _logger.LogWarning("Action: UpdatePrice | IP: {IpAddress} | Status: Failed - Car not found. | Car: {CarId} | User: {Username}", GetIpAddress(), id, GetUsername());
                return XmlErrorResponse(404, $"Car with id {id} not found.");
            }

            var validationErrors = _validationService.ValidateDealershipXml();
            if (validationErrors.Any())
            {
                var errorMsg = "Update breaks XSD validation: " + string.Join(" | ", validationErrors);
                _logger.LogWarning("Action: UpdatePrice | IP: {IpAddress} | Status: Failed - XSD validation broke. | User: {Username} | Errors: {Errors}", GetIpAddress(), GetUsername(), errorMsg);
                return XmlErrorResponse(400, errorMsg);
            }

            _logger.LogInformation("Action: UpdatePrice | IP: {IpAddress} | Status: Success | Car: {CarId} | New Price: {NewPrice} | User: {Username}", GetIpAddress(), id, update.NewPrice, GetUsername());
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action: UpdatePrice | IP: {IpAddress} | Status: Failed - Error updating car. | Car: {CarId} | User: {Username}", GetIpAddress(), id, GetUsername());
            return XmlErrorResponse(500, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// Removes a car from the inventory.
    /// </summary>
    /// <param name="id">The unique ID (VIN) of the car.</param>
    /// <response code="200">If the deletion was successful.</response>
    /// <response code="404">If the car is not found.</response>
    /// <response code="500">If there is an internal server error.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(XmlError), 404)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public IActionResult DeleteCar(string id)
    {
        try
        {
            var success = _dataService.DeleteCar(id);
            if (!success)
            {
                _logger.LogWarning("Action: DeleteCar | IP: {IpAddress} | Status: Failed - Car not found. | Car: {CarId} | User: {Username}", GetIpAddress(), id, GetUsername());
                return XmlErrorResponse(404, $"Car with id {id} not found.");
            }

            _logger.LogInformation("Action: DeleteCar | IP: {IpAddress} | Status: Success | Car: {CarId} | User: {Username}", GetIpAddress(), id, GetUsername());
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action: DeleteCar | IP: {IpAddress} | Status: Failed - Internal Error. | Car: {CarId} | User: {Username}", GetIpAddress(), id, GetUsername());
            return XmlErrorResponse(500, "An internal server error occurred.");
        }
    }

    /// <summary>
    /// Generates an HTML report of the XML inventory using an XSLT transformation. (Manager role required)
    /// </summary>
    /// <response code="200">Returns the formatted HTML report.</response>
    /// <response code="500">If the XSLT transformation fails.</response>
    [HttpGet("/report")]
    [Produces("text/html")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(typeof(XmlError), 500)]
    [Authorize(Roles = "Manager")] // Strict restriction: Only Managers can generate reports
    public IActionResult GetInventoryReport()
    {
        try
        {
            _logger.LogInformation("Action: GetInventoryReport | IP: {IpAddress} | Status: Success | Manager: {Username} generated HTML report.", GetIpAddress(), GetUsername());
            var html = _xsltService.GenerateHtmlReport();
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action: GetInventoryReport | IP: {IpAddress} | Status: Failed - XSLT error. | Manager: {Username}", GetIpAddress(), GetUsername());
            return XmlErrorResponse(500, "XSLT Transformation failed.");
        }
    }

    /// <summary>
    /// Executes an XPath query to retrieve all hybrid vehicles from the inventory.
    /// </summary>
    /// <response code="200">Returns a list of hybrid cars.</response>
    [HttpGet("xpath/hybrid")]
    [ProducesResponseType(typeof(List<Car>), 200)]
    public IActionResult GetHybridCars()
    {
        _logger.LogInformation("Action: GetHybridCars | IP: {IpAddress} | Status: Success | User: {Username} executed XPath query.", GetIpAddress(), GetUsername());
        return Ok(_dataService.GetHybridCars().ToList());
    }

    /// <summary>
    /// Queries the external NHTSA API to decode a VIN and generate an XML Car template.
    /// </summary>
    /// <param name="vin">The 17-character VIN.</param>
    /// <param name="year">Optional model year to increase decoding accuracy.</param>
    /// <response code="200">Returns the populated XML car template.</response>
    /// <response code="400">If the VIN breaks internal XSD rules.</response>
    /// <response code="500">If the external NHTSA API connection fails.</response>
    [HttpGet("decode-vin/{vin}")]
    [ProducesResponseType(typeof(Car), 200)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public async Task<IActionResult> DecodeVin(string vin, [FromQuery] string year = "")
    {
        try
        {
            _logger.LogInformation("Action: DecodeVin | IP: {IpAddress} | Status: Initiated | VIN: {Vin} | User: {Username}", GetIpAddress(), vin, GetUsername());
            var carTemplate = await _vinService.DecodeVinAndCreateCarTemplateAsync(vin, year);

            _logger.LogInformation("Action: DecodeVin | IP: {IpAddress} | Status: Success | VIN: {Vin} | User: {Username}", GetIpAddress(), vin, GetUsername());
            return Ok(carTemplate);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Action: DecodeVin | IP: {IpAddress} | Status: Failed - XSD rules broken for {Vin}. | User: {Username} | Error: {Error}", GetIpAddress(), vin, GetUsername(), ex.Message);
            return XmlErrorResponse(400, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action: DecodeVin | IP: {IpAddress} | Status: Failed - External API failure for {Vin}. | User: {Username}", GetIpAddress(), vin, GetUsername());
            return XmlErrorResponse(500, "External API integration failed.");
        }
    }
}

/// <summary>
/// Data transfer object for updating the price of a car.
/// </summary>
[System.Xml.Serialization.XmlRoot("PriceUpdate")]
public class PriceUpdate
{
    /// <summary>
    /// The new price to assign to the car. Must be greater than 0.
    /// </summary>
    [System.Xml.Serialization.XmlElement("newPrice")]
    public decimal NewPrice { get; set; }
}