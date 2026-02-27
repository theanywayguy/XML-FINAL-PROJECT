using System.Xml.Serialization;

namespace CarDealershipApi.Models;

/// <summary>
/// Internal representation of a user in the system.
/// </summary>
public class User
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// The XML payload required to authenticate a user.
/// </summary>
[XmlRoot("LoginRequest")]
public class LoginRequest
{
    /// <summary>
    /// The user's registered username.
    /// </summary>
    [XmlElement("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The user's password.
    /// </summary>
    [XmlElement("password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// The XML payload required by a Manager to register a new Salesperson.
/// </summary>
[XmlRoot("RegisterRequest")]
public class RegisterRequest
{
    /// <summary>
    /// The desired username for the new account.
    /// </summary>
    [XmlElement("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The secure password for the new account.
    /// </summary>
    [XmlElement("password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// The XML response containing the authentication token upon successful login.
/// </summary>
[XmlRoot("token")]
public class TokenResponse
{
    /// <summary>
    /// The JWT Bearer token used to authenticate subsequent API requests.
    /// </summary>
    [XmlElement("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// The lifetime of the token in seconds.
    /// </summary>
    [XmlElement("expiresIn")]
    public int ExpiresIn { get; set; }
}