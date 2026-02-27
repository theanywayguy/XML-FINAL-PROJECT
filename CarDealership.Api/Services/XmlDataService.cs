using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using CarDealershipApi.Models;

namespace CarDealershipApi.Services;

public class XmlDataService
{
    private readonly string _filePath;

    public XmlDataService(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "Data", "dealership.xml");
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
}