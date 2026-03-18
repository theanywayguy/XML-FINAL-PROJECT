using System.Xml.Serialization;
using CarDealershipApi.Models;
using CarDealershipApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarDealershipApi.Controllers.v1;
/// <summary>
/// Manages the dealership's vehicle inventory.
/// Exposes CRUD operations, named XPath query endpoints, and the
/// dynamic search endpoint — all returning application/xml.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/xml")]
[Authorize(Roles = "Manager,Salesperson")]
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

    // Helpers — keep controllers thin.
    private ObjectResult XmlErrorResponse(int code, string message) =>
        StatusCode(code, new XmlError { Code = code, Message = message });

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

    private string GetUsername() =>
        User.Identity?.Name ?? "Unknown";

    // =========================================================================
    // GET ENDPOINTS
    // =========================================================================

    /// <summary>Retrieves all cars currently in the inventory.</summary>
    /// <response code="200">Returns the full list of cars.</response>
    /// <response code="500">Unhandled server error — caught by GlobalExceptionMiddleware.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<Car>), 200)]
    [ProducesResponseType(typeof(XmlError), 500)]
    public IActionResult GetCars()
    {
        var cars = _dataService.GetAllCars().ToList();
        _logger.LogInformation(
            "Action: GetCars | IP: {IpAddress} | User: {Username} | Found {Count} cars.",
            GetIpAddress(), GetUsername(), cars.Count);
        return Ok(cars);
    }

    /// <summary>Retrieves a single car by its VIN.</summary>
    /// <param name="id">The 17-character VIN of the car.</param>
    /// <response code="200">Returns the requested car.</response>
    /// <response code="404">Car not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Car), 200)]
    [ProducesResponseType(typeof(XmlError), 404)]
    public IActionResult GetCar(string id)
    {
        var car = _dataService.GetCarById(id);
        if (car == null)
        {
            _logger.LogWarning(
                "Action: GetCar | IP: {IpAddress} | User: {Username} | Car {CarId} not found.",
                GetIpAddress(), GetUsername(), id);
            return XmlErrorResponse(404, $"Car with id {id} not found.");
        }

        _logger.LogInformation(
            "Action: GetCar | IP: {IpAddress} | User: {Username} | Retrieved car {CarId}.",
            GetIpAddress(), GetUsername(), id);
        return Ok(car);
    }

    /// <summary>
    /// Searches the inventory using a dynamic XPath predicate assembled from
    /// the supplied query parameters. All parameters are optional and combinable.
    /// Uses translate() for case-insensitive brand/model matching (XPath 1.0).
    /// </summary>
    /// <response code="200">Returns matching cars (empty list if none found).</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<Car>), 200)]
    public IActionResult SearchCars(
        [FromQuery] string? brand = null,
        [FromQuery] string? model = null,
        [FromQuery] int? year = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] bool? isHybrid = null)
    {
        var cars = _dataService.SearchCars(brand, model, year, minPrice, maxPrice, isHybrid).ToList();
        _logger.LogInformation(
            "Action: SearchCars | IP: {IpAddress} | User: {Username} | Brand: {Brand} Model: {Model} | Found {Count} cars.",
            GetIpAddress(), GetUsername(), brand, model, cars.Count);
        return Ok(cars);
    }

    /// <summary>
    /// XPath query 1 — Returns all cars with a price above the given threshold.
    /// Executes: //car[price &gt; {minPrice}]
    /// </summary>
    /// <param name="minPrice">Minimum price boundary (exclusive).</param>
    /// <response code="200">Returns matching cars.</response>
    [HttpGet("cars-above-price/{minPrice}")]
    [ProducesResponseType(typeof(List<Car>), 200)]
    public IActionResult GetCarsAbovePrice(decimal minPrice)
    {
        var cars = _dataService.GetCarsAbovePrice(minPrice).ToList();
        _logger.LogInformation(
            "Action: GetCarsAbovePrice | IP: {IpAddress} | User: {Username} | MinPrice: {MinPrice} | Found {Count} cars.",
            GetIpAddress(), GetUsername(), minPrice, cars.Count);
        return Ok(cars);
    }

    /// <summary>
    /// XPath query 2 — Returns all hybrid-powered vehicles.
    /// Executes: //car[engine/@type='hybrid']
    /// </summary>
    /// <response code="200">Returns hybrid cars.</response>
    [HttpGet("hybrid-cars")]
    [ProducesResponseType(typeof(List<Car>), 200)]
    public IActionResult GetHybridCars()
    {
        var cars = _dataService.GetHybridCars().ToList();
        _logger.LogInformation(
            "Action: GetHybridCars | IP: {IpAddress} | User: {Username} | Found {Count} hybrid cars.",
            GetIpAddress(), GetUsername(), cars.Count);
        return Ok(cars);
    }

    /// <summary>
    /// XPath query 3 — Returns the count of distinct customers who have made a purchase.
    /// Queries sales.xml: selects all //sale/customerId nodes and applies Distinct().
    /// </summary>
    /// <response code="200">Returns the unique customer count wrapped in an IntResult.</response>
    [Authorize(Roles = "Manager")]
    [HttpGet("customer-count")]
    [ProducesResponseType(typeof(IntResult), 200)]
    public IActionResult GetCustomerCount()
    {
        var count = _dataService.GetCustomerCount();
        _logger.LogInformation(
            "Action: GetCustomerCount | IP: {IpAddress} | User: {Username} | Count: {Count}.",
            GetIpAddress(), GetUsername(), count);
        return Ok(new IntResult { Value = count });
    }

    /// <summary>
    /// XPath query 4 — Returns sales volume per customer ID.
    /// For each distinct customerId in sales.xml, executes:
    /// count(//sale[customerId='{id}']) via XPathEvaluate.
    /// </summary>
    /// <response code="200">Returns a list of customer ID / sale count pairs.</response>
    [Authorize(Roles = "Manager")]
    [HttpGet("sales-per-customer")]
    [ProducesResponseType(typeof(SalesCountList), 200)]
    public IActionResult GetSalesPerCustomer()
    {
        var items = _dataService.GetSalesPerCustomer();
        _logger.LogInformation(
            "Action: GetSalesPerCustomer | IP: {IpAddress} | User: {Username} | Distinct customers: {Count}.",
            GetIpAddress(), GetUsername(), items.Count);
        return Ok(new SalesCountList { Items = items });
    }

    /// <summary>
    /// XPath query 5 — Returns sales volume grouped by payment method.
    /// For each distinct paymentMethod in sales.xml, executes:
    /// count(//sale[paymentMethod='{method}']) via XPathEvaluate.
    /// </summary>
    /// <response code="200">Returns a list of payment method / sale count pairs.</response>
    [Authorize(Roles = "Manager")]
    [HttpGet("sales-per-payment-method")]
    [ProducesResponseType(typeof(SalesCountList), 200)]
    public IActionResult GetSalesPerPaymentMethod()
    {
        var items = _dataService.GetSalesPerPaymentMethod();
        _logger.LogInformation(
            "Action: GetSalesPerPaymentMethod | IP: {IpAddress} | User: {Username} | Distinct methods: {Count}.",
            GetIpAddress(), GetUsername(), items.Count);
        return Ok(new SalesCountList { Items = items });
    }

    /// <summary>
    /// XPath query 6 — Returns all cars with a model year newer than the given threshold.
    /// Executes: //car[year &gt; {year}]
    /// </summary>
    /// <param name="year">Year threshold (exclusive).</param>
    /// <response code="200">Returns matching cars.</response>
    [HttpGet("cars-newer-than/{year}")]
    [ProducesResponseType(typeof(List<Car>), 200)]
    public IActionResult GetCarsNewerThan(int year)
    {
        var cars = _dataService.GetCarsNewerThan(year).ToList();
        _logger.LogInformation(
            "Action: GetCarsNewerThan | IP: {IpAddress} | User: {Username} | Year: {Year} | Found {Count} cars.",
            GetIpAddress(), GetUsername(), year, cars.Count);
        return Ok(cars);
    }

    /// <summary>
    /// Decodes a 17-character VIN via the external NHTSA vPIC API.
    /// Converts the JSON response to XML, validates it against vin-schema.xsd,
    /// and returns a pre-populated Car template ready to submit to POST /cars.
    /// </summary>
    /// <param name="vin">The 17-character Vehicle Identification Number.</param>
    /// <param name="year">Optional model year hint for improved decode accuracy.</param>
    /// <response code="200">Returns the populated Car XML template.</response>
    /// <response code="400">VIN response failed internal XSD validation.</response>
    [HttpGet("decode-vin/{vin}")]
    [ProducesResponseType(typeof(Car), 200)]
    [ProducesResponseType(typeof(XmlError), 400)]
    public async Task<IActionResult> DecodeVin(string vin, [FromQuery] string year = "")
    {
        // InvalidOperationException from VinIntegrationService (XSD failure) is caught
        // here because it maps to a 400, not a 500 — so we handle it explicitly.
        // Any other exception propagates to GlobalExceptionMiddleware → 500.
        try
        {
            var carTemplate = await _vinService.DecodeVinAndCreateCarTemplateAsync(vin, year);
            _logger.LogInformation(
                "Action: DecodeVin | IP: {IpAddress} | User: {Username} | VIN: {Vin} | Success.",
                GetIpAddress(), GetUsername(), vin);
            return Ok(carTemplate);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "Action: DecodeVin | IP: {IpAddress} | User: {Username} | VIN: {Vin} | XSD failure: {Error}.",
                GetIpAddress(), GetUsername(), vin, ex.Message);
            return XmlErrorResponse(400, ex.Message);
        }
    }

    // =========================================================================
    // POST / PUT / DELETE ENDPOINTS
    // =========================================================================

    /// <summary>
    /// Adds a new car to the inventory.
    /// After insertion, the entire dealership.xml is validated against schema.xsd.
    /// If validation fails, the insertion is automatically rolled back and a 400
    /// is returned containing all schema error messages.
    /// </summary>
    /// <param name="car">The car XML payload.</param>
    /// <response code="201">Car created — returns the new car object.</response>
    /// <response code="400">Invalid payload, missing fields, or XSD violation.</response>
    /// <response code="403">Caller does not have Manager role.</response>
    [HttpPost]
    [Authorize(Roles = "Manager")]
    [Consumes("application/xml")]
    [ProducesResponseType(typeof(Car), 201)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 403)]
    public IActionResult AddCar([FromBody] Car car)
    {
        if (car == null)
            return XmlErrorResponse(400, "Invalid XML payload.");

        // ArgumentException from AddCar (duplicate id, missing fields) maps to 400.
        try
        {
            _dataService.AddCar(car);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                "Action: AddCar | IP: {IpAddress} | User: {Username} | Rejected: {Message}.",
                GetIpAddress(), GetUsername(), ex.Message);
            return XmlErrorResponse(400, ex.Message);
        }

        // Post-write XSD validation with automatic rollback.
        var errors = _validationService.ValidateDealershipXml();
        if (errors.Any())
        {
            _dataService.DeleteCar(car.Id);
            var msg = "Added car breaks XSD validation: " + string.Join(" | ", errors);
            _logger.LogWarning(
                "Action: AddCar | IP: {IpAddress} | User: {Username} | XSD rollback: {Errors}.",
                GetIpAddress(), GetUsername(), msg);
            return XmlErrorResponse(400, msg);
        }

        _logger.LogInformation(
            "Action: AddCar | IP: {IpAddress} | User: {Username} | Created car {CarId}.",
            GetIpAddress(), GetUsername(), car.Id);
        return CreatedAtAction(nameof(GetCar), new { id = car.Id }, car);
    }

    /// <summary>
    /// Updates the price of an existing car.
    /// XSD validation runs after the update; if it fails the change is NOT
    /// automatically rolled back (price mutation is idempotent — safe to retry).
    /// </summary>
    /// <param name="id">The VIN of the car to update.</param>
    /// <param name="update">The new price payload.</param>
    /// <response code="200">Price updated successfully.</response>
    /// <response code="400">Invalid price value or XSD violation.</response>
    /// <response code="403">Caller does not have Manager role.</response>
    /// <response code="404">Car not found.</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "Manager")]
    [Consumes("application/xml")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 403)]
    [ProducesResponseType(typeof(XmlError), 404)]
    public IActionResult UpdateCarPrice(string id, [FromBody] PriceUpdate update)
    {
        if (update == null || update.NewPrice <= 0)
            return XmlErrorResponse(400, "Invalid price data in XML payload.");

        if (!_dataService.UpdateCarPrice(id, update.NewPrice))
        {
            _logger.LogWarning(
                "Action: UpdateCarPrice | IP: {IpAddress} | User: {Username} | Car {CarId} not found.",
                GetIpAddress(), GetUsername(), id);
            return XmlErrorResponse(404, $"Car with id {id} not found.");
        }

        var errors = _validationService.ValidateDealershipXml();
        if (errors.Any())
        {
            var msg = "Price update breaks XSD validation: " + string.Join(" | ", errors);
            _logger.LogWarning(
                "Action: UpdateCarPrice | IP: {IpAddress} | User: {Username} | XSD failure: {Errors}.",
                GetIpAddress(), GetUsername(), msg);
            return XmlErrorResponse(400, msg);
        }

        _logger.LogInformation(
            "Action: UpdateCarPrice | IP: {IpAddress} | User: {Username} | Car {CarId} → {NewPrice}.",
            GetIpAddress(), GetUsername(), id, update.NewPrice);
        return Ok();
    }

    /// <summary>
    /// Removes a car from the inventory.
    /// </summary>
    /// <param name="id">The VIN of the car to delete.</param>
    /// <response code="200">Car deleted successfully.</response>
    /// <response code="403">Caller does not have Manager role.</response>
    /// <response code="404">Car not found.</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(XmlError), 403)]
    [ProducesResponseType(typeof(XmlError), 404)]
    public IActionResult DeleteCar(string id)
    {
        if (!_dataService.DeleteCar(id))
        {
            _logger.LogWarning(
                "Action: DeleteCar | IP: {IpAddress} | User: {Username} | Car {CarId} not found.",
                GetIpAddress(), GetUsername(), id);
            return XmlErrorResponse(404, $"Car with id {id} not found.");
        }

        _logger.LogInformation(
            "Action: DeleteCar | IP: {IpAddress} | User: {Username} | Deleted car {CarId}.",
            GetIpAddress(), GetUsername(), id);
        return Ok();
    }
}

// ---------------------------------------------------------------------------
// DTO — kept in the same file since it is only used by this controller.
// ---------------------------------------------------------------------------

/// <summary>Request body for the PUT /cars/{id} price update endpoint.</summary>
[XmlRoot("PriceUpdate")]
public class PriceUpdate
{
    /// <summary>The new price to assign. Must be greater than zero.</summary>
    [XmlElement("newPrice")]
    public decimal NewPrice { get; set; }
}