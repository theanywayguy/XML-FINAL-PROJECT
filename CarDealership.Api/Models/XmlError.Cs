using System.Xml.Serialization;

namespace CarDealershipApi.Models;

/// <summary>
/// A standardized XML error response returned when an API request fails.
/// </summary>
[XmlRoot("error")]
public class XmlError
{
    /// <summary>
    /// The HTTP status code indicating the type of error (e.g., 400, 404, 500).
    /// </summary>
    [XmlElement("code")]
    public int Code { get; set; }

    /// <summary>
    /// A human-readable message describing what went wrong.
    /// </summary>
    [XmlElement("message")]
    public string Message { get; set; } = string.Empty;
}