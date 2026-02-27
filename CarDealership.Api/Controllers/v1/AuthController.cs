using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CarDealershipApi.Models;
using CarDealershipApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace CarDealershipApi.Controllers.v1;

/// <summary>
/// Handles authentication and user registration for the dealership API.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/xml")]
[Consumes("application/xml")]
public class AuthController : ControllerBase
{
    private readonly UserService _userService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserService userService, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _userService = userService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and issues a JWT access token.
    /// </summary>
    /// <param name="request">The login credentials (username and password).</param>
    /// <response code="200">Returns the JWT access token.</response>
    /// <response code="400">If the request XML is malformed or missing fields.</response>
    /// <response code="401">If the credentials are invalid.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), 200)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 401)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Extract the IP address of the requester
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Action: Login | IP: {IpAddress} | Status: Failed - Malformed XML or missing fields.", ipAddress);
            return StatusCode(400, new XmlError { Code = 400, Message = "Invalid XML or missing fields." });
        }

        var user = _userService.Authenticate(request.Username, request.Password);

        if (user == null)
        {
            _logger.LogWarning("Action: Login | IP: {IpAddress} | Status: Failed - Invalid credentials for user {Username}.", ipAddress, request.Username);
            return StatusCode(401, new XmlError { Code = 401, Message = "Invalid credentials" });
        }

        _logger.LogInformation("Action: Login | IP: {IpAddress} | Status: Success | User: {Username}", ipAddress, request.Username);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return Ok(new TokenResponse
        {
            AccessToken = tokenHandler.WriteToken(token),
            ExpiresIn = 1800
        });
    }

    /// <summary>
    /// Registers a new salesperson. (Manager role required)
    /// </summary>
    /// <param name="request">The desired username and password for the new account.</param>
    /// <response code="201">If the user was successfully registered.</response>
    /// <response code="400">If the request XML is malformed or missing fields.</response>
    /// <response code="401">If the requesting user is not authenticated.</response>
    /// <response code="403">If the requesting user is not a Manager.</response>
    /// <response code="409">If the username already exists.</response>
    [HttpPost("register")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(typeof(XmlError), 201)]
    [ProducesResponseType(typeof(XmlError), 400)]
    [ProducesResponseType(typeof(XmlError), 409)]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        // Extract the IP address of the requester
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Action: Register | IP: {IpAddress} | Status: Failed - Malformed XML or missing fields.", ipAddress);
            return StatusCode(400, new XmlError { Code = 400, Message = "Invalid XML or missing fields." });
        }

        var success = _userService.RegisterSalesperson(request.Username, request.Password);

        if (!success)
        {
            _logger.LogWarning("Action: Register | IP: {IpAddress} | Status: Failed - User {Username} already exists.", ipAddress, request.Username);
            return StatusCode(409, new XmlError { Code = 409, Message = "User already exists." });
        }

        // Get the manager who created this account
        var managerName = User.Identity?.Name ?? "Unknown";
        _logger.LogInformation("Action: Register | IP: {IpAddress} | Status: Success | Manager: {ManagerName} | New User: {Username}", ipAddress, managerName, request.Username);

        return StatusCode(201, new XmlError { Code = 201, Message = $"User {request.Username} registered successfully." });
    }
}