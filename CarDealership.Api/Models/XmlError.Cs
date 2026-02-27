using System.Xml.Serialization;

namespace CarDealershipApi.Models;

[XmlRoot("error")]
public class XmlError
{
    [XmlElement("code")]
    public int Code { get; set; }

    [XmlElement("message")]
    public string Message { get; set; } = string.Empty;
}