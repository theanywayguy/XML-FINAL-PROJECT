using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace CarDealershipApi.Models;

/// <summary>
/// Represents a vehicle in the dealership's inventory.
/// </summary>
[XmlRoot("Car")]
public class Car
{
    /// <summary>
    /// The unique 17-character Vehicle Identification Number (VIN).
    /// </summary>
    [XmlElement("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The manufacturer of the vehicle (e.g., Chevrolet, Ford, Toyota).
    /// </summary>
    [Required]
    [XmlElement("brand")]
    public string Brand { get; set; } = string.Empty;

    /// <summary>
    /// The specific model of the vehicle (e.g., Silverado, Mustang).
    /// </summary>
    [Required]
    [XmlElement("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// The four-digit manufacturing year of the vehicle.
    /// </summary>
    [XmlElement("year")]
    public int Year { get; set; }

    /// <summary>
    /// The pricing details for the vehicle.
    /// </summary>
    [XmlElement("price")]
    public Price Price { get; set; } = new();

    /// <summary>
    /// The engine specifications for the vehicle.
    /// </summary>
    [XmlElement("engine")]
    public Engine Engine { get; set; } = new();

    /// <summary>
    /// The total horsepower output of the engine.
    /// </summary>
    [XmlElement("horsepower")]
    public int Horsepower { get; set; }
}

/// <summary>
/// Represents the pricing information of a vehicle.
/// </summary>
public class Price
{
    /// <summary>
    /// The three-letter currency code (e.g., USD, EUR). Defaults to USD.
    /// </summary>
    [XmlElement("currency")]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// The numerical monetary value.
    /// </summary>
    [XmlElement("value")]
    public decimal Value { get; set; }
}

/// <summary>
/// Represents the engine details of a vehicle.
/// </summary>
public class Engine
{
    /// <summary>
    /// The fuel or power type (e.g., petrol, diesel, electric, hybrid).
    /// </summary>
    [XmlElement("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Additional description or nomenclature for the engine (e.g., 5.3L V8).
    /// </summary>
    [XmlElement("description")]
    public string Description { get; set; } = string.Empty;
}