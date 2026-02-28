using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using CarDealershipApi.Models;

namespace CarDealershipApi.Services;

public class XmlDataService
{
    private readonly string _salesFilePath;
    // The bouncer: Only allows 1 thread to access the files at a time to prevent corruption.
    private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
    private readonly string _filePath;

    public XmlDataService(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "Data", "dealership.xml");
        _salesFilePath = Path.Combine(env.ContentRootPath, "Data", "sales.xml");
    }

    private XDocument LoadDocument()
    {
        try
        {
            return XDocument.Load(_filePath);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Malformed XML detected: {ex.Message}");
        }
    }

    // --- CRUD OPERATIONS ---

    public IEnumerable<Car> GetAllCars()
    {
        var doc = LoadDocument();
        return doc.Descendants("car").Select(MapToCar);
    }

    public Car? GetCarById(string id)
    {
        var doc = LoadDocument();
        var carElement = doc.Descendants("car").FirstOrDefault(c => c.Attribute("id")?.Value == id);
        return carElement != null ? MapToCar(carElement) : null;
    }

    public void AddCar(Car car)
    {
        if (string.IsNullOrWhiteSpace(car.Id) || string.IsNullOrWhiteSpace(car.Brand))
            throw new ArgumentException("Missing required fields: 'id' and 'brand' are mandatory.");

        var doc = LoadDocument();
        var carsElement = doc.Root?.Element("cars");

        if (carsElement == null) throw new InvalidOperationException("Invalid XML structure: missing <cars> element.");

        if (doc.Descendants("car").Any(c => c.Attribute("id")?.Value == car.Id))
            throw new ArgumentException($"Car with id '{car.Id}' already exists.");

        var newCar = new XElement("car",
            new XAttribute("id", car.Id),
            new XElement("brand", car.Brand),
            new XElement("model", car.Model),
            new XElement("year", car.Year),
            new XElement("price", new XAttribute("currency", car.Price.Currency), car.Price.Value),
            new XElement("engine", new XAttribute("type", car.Engine.Type), car.Engine.Description),
            new XElement("horsepower", car.Horsepower)
        );

        carsElement.Add(newCar);
        doc.Save(_filePath);
    }

    public bool UpdateCarPrice(string id, decimal newPrice)
    {
        var doc = LoadDocument();
        var carElement = doc.Descendants("car").FirstOrDefault(c => c.Attribute("id")?.Value == id);

        if (carElement == null) return false;

        var priceElement = carElement.Element("price");
        if (priceElement != null)
        {
            priceElement.Value = newPrice.ToString("F2");
            doc.Save(_filePath);
            return true;
        }
        return false;
    }

    public bool DeleteCar(string id)
    {
        var doc = LoadDocument();
        var carElement = doc.Descendants("car").FirstOrDefault(c => c.Attribute("id")?.Value == id);

        if (carElement == null) return false;

        carElement.Remove();
        doc.Save(_filePath);
        return true;
    }

    // --- XPATH QUERIES ---

    public IEnumerable<Car> GetCarsAbovePrice(decimal minPrice)
    {
        var doc = LoadDocument();
        var elements = doc.XPathSelectElements($"//car[price > {minPrice}]");
        return elements.Select(MapToCar);
    }

    public IEnumerable<Car> GetHybridCars()
    {
        var doc = LoadDocument();
        var elements = doc.XPathSelectElements("//car[engine/@type='hybrid']");
        return elements.Select(MapToCar);
    }

    public int GetCustomerCount()
    {
        var doc = LoadDocument();
        var count = doc.XPathEvaluate("count(//customer)");
        return Convert.ToInt32(count);
    }

    public Dictionary<string, int> GetSalesPerCustomer()
    {
        var doc = LoadDocument();
        var salesData = new Dictionary<string, int>();
        var customers = doc.XPathSelectElements("//customer");

        foreach (var customer in customers)
        {
            var customerId = customer.Attribute("id")?.Value;
            if (customerId != null)
            {
                var salesCount = Convert.ToInt32(doc.XPathEvaluate($"count(//sale[@customerId='{customerId}'])"));
                var name = customer.Element("name")?.Value ?? customerId;
                salesData.Add(name, salesCount);
            }
        }
        return salesData;
    }

    public IEnumerable<Car> GetCarsNewerThan(int year)
    {
        var doc = LoadDocument();
        var elements = doc.XPathSelectElements($"//car[year > {year}]");
        return elements.Select(MapToCar);
    }

    // --- HELPER METHOD ---

    private Car MapToCar(XElement element)
    {
        return new Car
        {
            Id = element.Attribute("id")?.Value ?? string.Empty,
            Brand = element.Element("brand")?.Value ?? string.Empty,
            Model = element.Element("model")?.Value ?? string.Empty,
            Year = int.TryParse(element.Element("year")?.Value, out var y) ? y : 0,
            Price = new Price
            {
                Currency = element.Element("price")?.Attribute("currency")?.Value ?? "USD",
                Value = decimal.TryParse(element.Element("price")?.Value, out var p) ? p : 0
            },
            Engine = new Engine
            {
                Type = element.Element("engine")?.Attribute("type")?.Value ?? string.Empty,
                Description = element.Element("engine")?.Value ?? string.Empty
            },
            Horsepower = int.TryParse(element.Element("horsepower")?.Value, out var hp) ? hp : 0
        };
    }

    /// <summary>
    /// Dynamically constructs and executes an XPath query to search the inventory.
    /// </summary>
    public IEnumerable<Car> SearchCars(string? brand, string? model, int? year, decimal? minPrice, decimal? maxPrice, bool? isHybrid)
    {
        // Use your existing helper to load the document safely
        var doc = LoadDocument();

        // Start building our XPath conditions
        var conditions = new List<string>();

        // Use 'brand' to match your XML model
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var safeBrand = brand.Replace("'", "");
            // Case-insensitive XPath 1.0 matching
            conditions.Add($"brand[translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') = '{safeBrand.ToLower()}']");
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            var safeModel = model.Replace("'", "");
            conditions.Add($"model[translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') = '{safeModel.ToLower()}']");
        }

        if (year.HasValue)
        {
            conditions.Add($"year = {year.Value}");
        }

        if (minPrice.HasValue)
        {
            conditions.Add($"price >= {minPrice.Value}");
        }

        if (maxPrice.HasValue)
        {
            conditions.Add($"price <= {maxPrice.Value}");
        }

        // Hybrid logic mapped to the <engine type="hybrid"> attribute
        if (isHybrid.HasValue && isHybrid.Value)
        {
            conditions.Add("engine/@type = 'hybrid'");
        }
        else if (isHybrid.HasValue && !isHybrid.Value)
        {
            conditions.Add("engine/@type != 'hybrid'");
        }

        // Combine the conditions into a single XPath string
        // Using lowercase 'car' to perfectly match your existing XML document structure
        string xpath = "//car";

        if (conditions.Any())
        {
            xpath += "[" + string.Join(" and ", conditions) + "]";
        }

        // Execute the XPath query
        var elements = doc.XPathSelectElements(xpath);

        // Reuse your existing helper method to parse the results!
        return elements.Select(MapToCar);
    }


    /// <summary>
    /// Safely moves a car from the inventory file to the sales file.
    /// </summary>
    public async Task<bool> ProcessSaleAsync(Sale sale)
    {
        // Wait for the lock. If another sale is happening, this thread pauses here.
        await _fileLock.WaitAsync();
        try
        {
            var inventoryDoc = LoadDocument();
            var carElement = inventoryDoc.Descendants("car").FirstOrDefault(c => c.Attribute("id")?.Value == sale.CarId);

            // 1. Validate: Does the car exist in inventory?
            if (carElement == null) return false;

            // 2. Load or Create sales.xml
            XDocument salesDoc;
            if (File.Exists(_salesFilePath))
            {
                salesDoc = XDocument.Load(_salesFilePath);
            }
            else
            {
                salesDoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), new XElement("sales"));
            }

            // 3. Create the Sale XML Element
            var newSaleElement = new XElement("sale",
                new XAttribute("id", sale.SaleId),
                new XElement("salesmanId", sale.SalesmanId),
                new XElement("customerId", sale.CustomerId), // Mapped CustomerId!
                new XElement("carId", sale.CarId),
                new XElement("dateTime", sale.DateTime.ToString("O")), // ISO 8601 format
                new XElement("paymentMethod", sale.PaymentMethod.ToString()),
                new XElement("price", sale.Price.ToString("F2")),
                new XElement(carElement) // THE MAGIC TRICK: We archive the whole car node inside the sale!
            );

            // 4. Execution: Add to sales, remove from inventory
            salesDoc.Root?.Add(newSaleElement);
            carElement.Remove();

            // 5. Commit: Save both files safely
            salesDoc.Save(_salesFilePath);
            inventoryDoc.Save(_filePath);

            return true;
        }
        finally
        {
            // ALWAYS release the lock, even if the code crashes, so the API doesn't freeze forever.
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Reverts a sale by moving the archived car back to inventory and deleting the sale record.
    /// Fails if the sale is older than 3 hours.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> RevertSaleAsync(string saleId)
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_salesFilePath)) return (false, "Sales database not found.");

            var salesDoc = XDocument.Load(_salesFilePath);
            var saleElement = salesDoc.Descendants("sale").FirstOrDefault(s => s.Attribute("id")?.Value == saleId);

            if (saleElement == null) return (false, $"Sale with ID '{saleId}' not found.");

            // 1. Check the 3-hour window
            var dateTimeStr = saleElement.Element("dateTime")?.Value;
            if (DateTime.TryParse(dateTimeStr, out DateTime saleDate))
            {
                if ((DateTime.UtcNow - saleDate).TotalHours > 3)
                {
                    return (false, "The 3-hour window to revert this sale has expired.");
                }
            }

            // 2. Extract the archived car element
            var archivedCarElement = saleElement.Element("car");
            if (archivedCarElement == null)
            {
                return (false, "Corrupted sale record: Missing archived car data. Cannot revert.");
            }

            // 3. Load Dealership Inventory
            var inventoryDoc = LoadDocument();
            var carsRoot = inventoryDoc.Root?.Element("cars") ?? inventoryDoc.Root;

            if (carsRoot == null) return (false, "Corrupted dealership inventory: Missing <cars> root node.");

            // 4. Execution: Put car back in inventory, remove the sale
            carsRoot.Add(new XElement(archivedCarElement)); // Clone it back into inventory
            saleElement.Remove();

            // 5. Commit: Save both files safely
            inventoryDoc.Save(_filePath);
            salesDoc.Save(_salesFilePath);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Internal error during revert: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }
}