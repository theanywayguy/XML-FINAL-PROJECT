using System.Xml.Serialization;

namespace CarDealershipApi.Models;

/// <summary>
/// Internal representation of a user in the system.
/// </summary>
public class User
{
    [XmlElement("id")]
    public string Id { get; set; } = string.Empty;

    [XmlElement("username")]
    public string Username { get; set; } = string.Empty;

    [XmlElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [XmlElement("role")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// The XML payload required to authenticate a user.
/// </summary>
[XmlRoot("LoginRequest")]
public class LoginRequest
{
    [XmlElement("username")]
    public string Username { get; set; } = string.Empty;

    [XmlElement("password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// The XML payload required by a Manager to register a new Salesperson.
/// </summary>
[XmlRoot("RegisterRequest")]
public class RegisterRequest
{
    [XmlElement("username")]
    public string Username { get; set; } = string.Empty;

    [XmlElement("password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// The XML response containing the authentication token upon successful login.
/// </summary>
[XmlRoot("token")]
public class TokenResponse
{
    [XmlElement("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [XmlElement("expiresIn")]
    public int ExpiresIn { get; set; }
}