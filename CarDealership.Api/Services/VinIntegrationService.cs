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

        // Safely extract values (NHTSA returns empty strings for missing data)
        var make = results.GetProperty("Make").GetString() ?? "Unknown";
        var model = results.GetProperty("Model").GetString() ?? "Unknown";
        var yearStr = results.GetProperty("ModelYear").GetString();
        var fuelType = results.GetProperty("FuelTypePrimary").GetString() ?? "Unknown";

        // NHTSA API flattens horsepower under EngineHP or EngineBrake_hp depending on the exact vehicle.
        var hpStr = results.GetProperty("EngineHP").GetString();
        if (string.IsNullOrWhiteSpace(hpStr)) hpStr = results.GetProperty("EngineBrake_hp").GetString();

        var year = int.TryParse(yearStr, out var y) ? y : DateTime.Now.Year;
        var hp = int.TryParse(hpStr, out var h) ? h : 0; // Default to 0 if NHTSA lacks data

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