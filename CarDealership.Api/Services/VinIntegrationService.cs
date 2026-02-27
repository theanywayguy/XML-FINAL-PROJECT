using System.Text.Json;
using System.Xml.Linq;
using CarDealershipApi.Models;

namespace CarDealershipApi.Services;

public class VinIntegrationService
{
    private readonly HttpClient _httpClient;
    private readonly XsdValidationService _validationService;
    private readonly string _vinSchemaPath;

    public VinIntegrationService(HttpClient httpClient, XsdValidationService validationService, IWebHostEnvironment env)
    {
        _httpClient = httpClient;
        _validationService = validationService;
        _vinSchemaPath = Path.Combine(env.ContentRootPath, "Data", "vin-schema.xsd");
    }

    public async Task<Car> DecodeVinAndCreateCarTemplateAsync(string vin, string modelYear = "")
    {
        // 1. Call external REST API (JSON format requested to fulfill requirement)
        var url = $"https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValues/{vin}?format=json";
        if (!string.IsNullOrEmpty(modelYear)) url += $"&modelyear={modelYear}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(jsonString);
        var results = jsonDoc.RootElement.GetProperty("Results")[0];

        // Safely extract Make, Model, and Year (catching empty strings)
        var make = results.TryGetProperty("Make", out var makeProp) ? makeProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(make)) make = "Unknown";

        var model = results.TryGetProperty("Model", out var modelProp) ? modelProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(model)) model = "Unknown";

        var yearStr = results.TryGetProperty("ModelYear", out var yearProp) ? yearProp.GetString() : null;
        var year = int.TryParse(yearStr, out var y) ? y : DateTime.Now.Year;

        // Safely extract Fuel Type (catching empty strings)
        var fuelType = results.TryGetProperty("FuelTypePrimary", out var fuelProp) ? fuelProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(fuelType)) fuelType = "Unknown";

        // Safely extract and parse Horsepower (handling decimals like "315.0000")
        string? hpStr = null;
        if (results.TryGetProperty("EngineHP", out var hpProp)) hpStr = hpProp.GetString();
        if (string.IsNullOrWhiteSpace(hpStr) && results.TryGetProperty("EngineBrake_hp", out var brakeHpProp)) hpStr = brakeHpProp.GetString();

        int hp = 0;
        if (!string.IsNullOrWhiteSpace(hpStr) && double.TryParse(hpStr, out var parsedHp))
        {
            hp = (int)Math.Round(parsedHp); // Converts 315.0000 to 315
        }

        // 2. Convert JSON response data to XML
        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("DecodedVin", new XAttribute("vin", vin),
                new XElement("Make", make),
                new XElement("Model", model),
                new XElement("Year", year),
                new XElement("FuelType", fuelType),
                new XElement("Horsepower", hp)
            )
        );

        // 3. Validate generated XML against XSD
        var xmlString = xmlDoc.ToString();
        var validationErrors = _validationService.ValidateXmlString(xmlString, _vinSchemaPath);

        if (validationErrors.Any())
        {
            throw new InvalidOperationException("External API XML conversion failed XSD validation:\n" + string.Join("\n", validationErrors));
        }

        // 4. Integrate into system (Return a Car template)
        return new Car
        {
            Id = vin,
            Brand = make,
            Model = model,
            Year = year,
            Horsepower = hp,
            Engine = new Engine { Description = fuelType, Type = DetermineEngineType(fuelType) }
        };
    }

    private string DetermineEngineType(string fuelType)
    {
        var fuelLower = fuelType.ToLower();
        if (fuelLower.Contains("electric")) return "electric";
        if (fuelLower.Contains("hybrid")) return "hybrid";
        if (fuelLower.Contains("diesel")) return "diesel";
        return "petrol"; // default mapping for Gasoline
    }
}