using CarDealershipApi.Models;
using CarDealershipApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarDealershipApi.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/xml")]
[Consumes("application/xml")]
public class CarsController : ControllerBase
{
    private readonly XmlDataService _dataService;
    private readonly XsdValidationService _validationService;
    private readonly XsltTransformationService _xsltService;
    private readonly VinIntegrationService _vinService;

    public CarsController(
        XmlDataService dataService,
        XsdValidationService validationService,
        XsltTransformationService xsltService,
        VinIntegrationService vinService)
    {
        _dataService = dataService;
        _validationService = validationService;
        _xsltService = xsltService;
        _vinService = vinService;
    }

    private ObjectResult XmlErrorResponse(int code, string message)
    {
        return StatusCode(code, new XmlError { Code = code, Message = message });
    }

    [HttpGet]
    public IActionResult GetCars()
    {
        try
        {
            var cars = _dataService.GetAllCars().ToList();
            return Ok(cars);
        }
        catch (Exception ex)
        {
            return XmlErrorResponse(500, ex.Message);
        }
    }

    [HttpGet("{id}")]
    public IActionResult GetCar(string id)
    {
        try
        {
            var car = _dataService.GetCarById(id);
            if (car == null) return XmlErrorResponse(404, $"Car with id {id} not found.");
            return Ok(car);
        }
        catch (Exception ex)
        {
            return XmlErrorResponse(500, ex.Message);
        }
    }

    [HttpPost]
    public IActionResult AddCar([FromBody] Car car)
    {
        try
        {
            if (car == null) return XmlErrorResponse(400, "Invalid XML payload.");

            _dataService.AddCar(car);

            // Validate the entire document after insertion to ensure academic strictness
            var validationErrors = _validationService.ValidateDealershipXml();
            if (validationErrors.Any())
            {
                // Rollback (delete) since it broke schema
                _dataService.DeleteCar(car.Id);
                return XmlErrorResponse(400, "Added car breaks XSD validation: " + string.Join(" | ", validationErrors));
            }

            return CreatedAtAction(nameof(GetCar), new { id = car.Id }, car);
        }
        catch (ArgumentException ex)
        {
            return XmlErrorResponse(400, ex.Message);
        }
        catch (Exception ex)
        {
            return XmlErrorResponse(500, ex.Message);
        }
    }

    [HttpPut("{id}")]
    public IActionResult UpdateCarPrice(string id, [FromBody] PriceUpdateDto update)
    {
        try
        {
            if (update == null || update.NewPrice <= 0)
                return XmlErrorResponse(400, "Invalid price data in XML.");

            var success = _dataService.UpdateCarPrice(id, update.NewPrice);
            if (!success) return XmlErrorResponse(404, $"Car with id {id} not found.");

            var validationErrors = _validationService.ValidateDealershipXml();
            if (validationErrors.Any())
            {
                return XmlErrorResponse(400, "Update breaks XSD validation: " + string.Join(" | ", validationErrors));
            }

            return Ok();
        }
        catch (Exception ex)
        {
            return XmlErrorResponse(500, ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteCar(string id)
    {
        try
        {
            var success = _dataService.DeleteCar(id);
            if (!success) return XmlErrorResponse(404, $"Car with id {id} not found.");
            return Ok();
        }
        catch (Exception ex)
        {
            return XmlErrorResponse(500, ex.Message);
        }
    }

    [HttpGet("/report")]
    [Produces("text/html")]
    public IActionResult GetInventoryReport()
    {
        try
        {
            var html = _xsltService.GenerateHtmlReport();
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return XmlErrorResponse(500, "XSLT Transformation failed: " + ex.Message);
        }
    }

    [HttpGet("xpath/hybrid")]
    public IActionResult GetHybridCars()
    {
        return Ok(_dataService.GetHybridCars().ToList());
    }

    [HttpGet("decode-vin/{vin}")]
    public async Task<IActionResult> DecodeVin(string vin, [FromQuery] string year = "")
    {
        try
        {
            var carTemplate = await _vinService.DecodeVinAndCreateCarTemplateAsync(vin, year);
            return Ok(carTemplate);
        }
        catch (InvalidOperationException ex)
        {
            return XmlErrorResponse(400, ex.Message);
        }
        catch (Exception ex)
        {
            return XmlErrorResponse(500, "External API integration failed: " + ex.Message);
        }
    }
}

[System.Xml.Serialization.XmlRoot("priceUpdate")]
public class PriceUpdateDto
{
    [System.Xml.Serialization.XmlElement("newPrice")]
    public decimal NewPrice { get; set; }
}