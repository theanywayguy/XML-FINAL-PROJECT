using System.Xml.Serialization;

namespace CarDealershipApi.Models;

[XmlRoot("car")]
public class Car
{
    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;

    [XmlElement("brand")]
    public string Brand { get; set; } = string.Empty;

    [XmlElement("model")]
    public string Model { get; set; } = string.Empty;

    [XmlElement("year")]
    public int Year { get; set; }

    [XmlElement("price")]
    public Price Price { get; set; } = new();

    [XmlElement("engine")]
    public Engine Engine { get; set; } = new();

    [XmlElement("horsepower")]
    public int Horsepower { get; set; }
}

public class Price
{
    [XmlAttribute("currency")]
    public string Currency { get; set; } = "USD";

    [XmlText]
    public decimal Value { get; set; }
}

public class Engine
{
    [XmlAttribute("type")]
    public string Type { get; set; } = string.Empty;

    [XmlText]
    public string Description { get; set; } = string.Empty;
}