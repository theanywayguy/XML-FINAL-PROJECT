using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace CarDealershipApi.Models;

/// <summary>
/// Strictly defined payment methods. The API will reject anything else.
/// </summary>
public enum PaymentMethod
{
    Cash,
    Credit,
    Financed
}

/// <summary>
/// The DTO representing the incoming sale request from the client.
/// </summary>
[XmlRoot("SaleRequest")]
public class SaleRequest
{
    [XmlElement("carId")]
    [Required]
    public string CarId { get; set; } = string.Empty;

    // Added Customer ID
    [XmlElement("customerId")]
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [XmlElement("price")]
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    public decimal Price { get; set; }

    [XmlElement("paymentMethod")]
    [Required]
    public PaymentMethod PaymentMethod { get; set; }
}

/// <summary>
/// The internal model that gets saved to sales.xml.
/// </summary>
[XmlRoot("Sale")]
public class Sale
{
    [XmlElement("saleId")]
    public string SaleId { get; set; } = string.Empty;

    [XmlElement("salesmanId")]
    public string SalesmanId { get; set; } = string.Empty;

    // Added Customer ID
    [XmlElement("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [XmlElement("carId")]
    public string CarId { get; set; } = string.Empty;

    [XmlElement("dateTime")]
    public DateTime DateTime { get; set; }

    [XmlElement("paymentMethod")]
    public PaymentMethod PaymentMethod { get; set; }

    [XmlElement("price")]
    public decimal Price { get; set; }
}


[XmlRoot("SaleResponse")]
public class SaleResponse
{
    [XmlElement("saleId")]
    public string SaleId { get; set; } = string.Empty;
}