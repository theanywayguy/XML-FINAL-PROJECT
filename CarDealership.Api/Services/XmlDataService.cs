using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using CarDealershipApi.Models;

namespace CarDealershipApi.Services;

public class XmlDataService
{
    // Only one thread may read/write the XML files at a time — prevents corruption during concurrent sales.
    private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

    private readonly string _filePath;
    private readonly string _salesFilePath;

    public XmlDataService(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "Data", "dealership.xml");
        _salesFilePath = Path.Combine(env.ContentRootPath, "Data", "sales.xml");
    }

    // -------------------------------------------------------------------------
    // PRIVATE HELPERS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads dealership.xml and wraps any parse failure in a meaningful exception
    /// that the GlobalExceptionMiddleware will catch and return as a 500 XML error.
    /// </summary>
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

    /// <summary>
    /// Maps a single &lt;car&gt; XElement to the Car model.
    /// Uses null-conditional access and TryParse throughout so a malformed node
    /// never throws — it returns sensible defaults instead.
    /// This is the single mapping site for the entire application.
    /// </summary>
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

    // -------------------------------------------------------------------------
    // CRUD OPERATIONS
    // -------------------------------------------------------------------------

    public IEnumerable<Car> GetAllCars()
    {
        return LoadDocument().Descendants("car").Select(MapToCar);
    }

    public Car? GetCarById(string id)
    {
        var element = LoadDocument()
            .Descendants("car")
            .FirstOrDefault(c => c.Attribute("id")?.Value == id);

        return element != null ? MapToCar(element) : null;
    }

    public void AddCar(Car car)
    {
        if (string.IsNullOrWhiteSpace(car.Id) || string.IsNullOrWhiteSpace(car.Brand))
            throw new ArgumentException("Missing required fields: 'id' and 'brand' are mandatory.");

        var doc = LoadDocument();
        var carsElement = doc.Root?.Element("cars")
            ?? throw new InvalidOperationException("Invalid XML structure: missing <cars> element.");

        if (doc.Descendants("car").Any(c => c.Attribute("id")?.Value == car.Id))
            throw new ArgumentException($"Car with id '{car.Id}' already exists.");

        // Functional LINQ to XML construction — mirrors the intended XML structure in code.
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
        if (priceElement == null) return false;

        // Targeted mutation — only the <price> text content changes, all other nodes are untouched.
        priceElement.Value = newPrice.ToString("F2");
        doc.Save(_filePath);
        return true;
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

    // -------------------------------------------------------------------------
    // XPATH QUERIES
    // -------------------------------------------------------------------------

    /// <summary>XPath query 1 — Filter inventory by minimum price threshold.</summary>
    public IEnumerable<Car> GetCarsAbovePrice(decimal minPrice)
    {
        return LoadDocument()
            .XPathSelectElements($"//car[price > {minPrice}]")
            .Select(MapToCar);
    }

    /// <summary>XPath query 2 — Retrieve all hybrid-powered vehicles.</summary>
    public IEnumerable<Car> GetHybridCars()
    {
        return LoadDocument()
            .XPathSelectElements("//car[engine/@type='hybrid']")
            .Select(MapToCar);
    }

    /// <summary>
    /// XPath query 3 — Count of distinct customers who have made at least one purchase.
    /// Queries sales.xml using XPathSelectElements to select all customerId nodes,
    /// then applies Distinct() to count unique buyers.
    /// </summary>
    public int GetCustomerCount()
    {
        if (!File.Exists(_salesFilePath)) return 0;

        return XDocument.Load(_salesFilePath)
            .XPathSelectElements("//sale/customerId")
            .Select(e => e.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Count();
    }

    /// <summary>
    /// XPath query 4 — Sales volume grouped by customer ID.
    /// For each distinct customerId found in sales.xml, XPathEvaluate runs
    /// count(//sale[customerId='{id}']) to aggregate the total per buyer.
    /// </summary>
    public List<SalesCountEntry> GetSalesPerCustomer()
    {
        if (!File.Exists(_salesFilePath)) return new List<SalesCountEntry>();

        var salesDoc = XDocument.Load(_salesFilePath);

        return salesDoc
            .XPathSelectElements("//sale/customerId")
            .Select(e => e.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Select(id => new SalesCountEntry
            {
                Key = id,
                Count = Convert.ToInt32(salesDoc.XPathEvaluate($"count(//sale[customerId='{id}'])"))
            })
            .ToList();
    }

    /// <summary>
    /// XPath query 5 — Sales volume grouped by payment method.
    /// For each distinct paymentMethod in sales.xml, XPathEvaluate runs
    /// count(//sale[paymentMethod='{method}']) to tally each payment type.
    /// </summary>
    public List<SalesCountEntry> GetSalesPerPaymentMethod()
    {
        if (!File.Exists(_salesFilePath)) return new List<SalesCountEntry>();

        var salesDoc = XDocument.Load(_salesFilePath);

        return salesDoc
            .XPathSelectElements("//sale/paymentMethod")
            .Select(e => e.Value)
            .Where(pm => !string.IsNullOrWhiteSpace(pm))
            .Distinct()
            .Select(method => new SalesCountEntry
            {
                Key = method,
                Count = Convert.ToInt32(salesDoc.XPathEvaluate($"count(//sale[paymentMethod='{method}'])"))
            })
            .ToList();
    }

    /// <summary>XPath query 6 — Filter inventory by minimum model year.</summary>
    public IEnumerable<Car> GetCarsNewerThan(int year)
    {
        return LoadDocument()
            .XPathSelectElements($"//car[year > {year}]")
            .Select(MapToCar);
    }

    /// <summary>
    /// Dynamic XPath builder — powers GET /api/v1/cars/search.
    /// Assembles a multi-condition XPath predicate at runtime from whatever
    /// query parameters are present. Uses translate() for case-insensitive
    /// brand/model matching (XPath 1.0 has no native lower-case() function).
    /// Single quotes are stripped from user input to prevent XPath injection.
    /// </summary>
    public IEnumerable<Car> SearchCars(
        string? brand, string? model, int? year,
        decimal? minPrice, decimal? maxPrice, bool? isHybrid)
    {
        var doc = LoadDocument();
        var conditions = new List<string>();

        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";

        if (!string.IsNullOrWhiteSpace(brand))
        {
            var safe = brand.Replace("'", "");
            conditions.Add($"brand[translate(text(),'{upper}','{lower}')='{safe.ToLower()}']");
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            var safe = model.Replace("'", "");
            conditions.Add($"model[translate(text(),'{upper}','{lower}')='{safe.ToLower()}']");
        }

        if (year.HasValue) conditions.Add($"year = {year.Value}");
        if (minPrice.HasValue) conditions.Add($"price >= {minPrice.Value}");
        if (maxPrice.HasValue) conditions.Add($"price <= {maxPrice.Value}");

        if (isHybrid.HasValue)
            conditions.Add(isHybrid.Value
                ? "engine/@type = 'hybrid'"
                : "engine/@type != 'hybrid'");

        var xpath = "//car";
        if (conditions.Any())
            xpath += "[" + string.Join(" and ", conditions) + "]";

        return doc.XPathSelectElements(xpath).Select(MapToCar);
    }

    // -------------------------------------------------------------------------
    // SALE TRANSACTIONS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Atomically moves a car from dealership.xml to sales.xml.
    /// The full &lt;car&gt; element is archived inside the &lt;sale&gt; record,
    /// enabling lossless reversion without any future lookup.
    /// Protected by a static SemaphoreSlim so concurrent HTTP requests
    /// cannot interleave writes and corrupt either file.
    /// </summary>
    public async Task<bool> ProcessSaleAsync(Sale sale)
    {
        await _fileLock.WaitAsync();
        try
        {
            var inventoryDoc = LoadDocument();
            var carElement = inventoryDoc.Descendants("car")
                .FirstOrDefault(c => c.Attribute("id")?.Value == sale.CarId);

            if (carElement == null) return false;

            // Load or create sales.xml.
            var salesDoc = File.Exists(_salesFilePath)
                ? XDocument.Load(_salesFilePath)
                : new XDocument(new XDeclaration("1.0", "utf-8", "yes"), new XElement("sales"));

            var newSaleElement = new XElement("sale",
                new XAttribute("id", sale.SaleId),
                new XElement("salesmanId", sale.SalesmanId),
                new XElement("customerId", sale.CustomerId),
                new XElement("carId", sale.CarId),
                new XElement("dateTime", sale.DateTime.ToString("O")),
                new XElement("paymentMethod", sale.PaymentMethod.ToString()),
                new XElement("price", sale.Price.ToString("F2")),
                new XElement(carElement)  // Full car snapshot archived inside the sale record.
            );

            salesDoc.Root?.Add(newSaleElement);
            carElement.Remove();

            salesDoc.Save(_salesFilePath);
            inventoryDoc.Save(_filePath);

            return true;
        }
        finally
        {
            // Always released — even on exception — so the API never freezes.
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Reverts a sale within the 3-hour window.
    /// Clones the archived &lt;car&gt; element back into dealership.xml and
    /// removes the sale record from sales.xml — all inside the same lock
    /// as ProcessSaleAsync, so revert and sale operations are mutually exclusive.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> RevertSaleAsync(string saleId)
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_salesFilePath))
                return (false, "Sales database not found.");

            var salesDoc = XDocument.Load(_salesFilePath);
            var saleElement = salesDoc.Descendants("sale")
                .FirstOrDefault(s => s.Attribute("id")?.Value == saleId);

            if (saleElement == null)
                return (false, $"Sale with ID '{saleId}' not found.");

            // Enforce the 3-hour revert window.
            var dateTimeStr = saleElement.Element("dateTime")?.Value;
            if (DateTime.TryParse(dateTimeStr, out var saleDate) &&
                (DateTime.UtcNow - saleDate).TotalHours > 3)
            {
                return (false, "The 3-hour window to revert this sale has expired.");
            }

            var archivedCarElement = saleElement.Element("car")
                ?? throw new InvalidOperationException("Corrupted sale record: missing archived car data.");

            var inventoryDoc = LoadDocument();
            var carsRoot = inventoryDoc.Root?.Element("cars")
                ?? throw new InvalidOperationException("Corrupted dealership inventory: missing <cars> element.");

            // Clone the archived car back into inventory, then remove the sale record.
            carsRoot.Add(new XElement(archivedCarElement));
            saleElement.Remove();

            inventoryDoc.Save(_filePath);
            salesDoc.Save(_salesFilePath);

            return (true, string.Empty);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}