using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CarDealershipApi.Tests;

public class CarsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CarsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Helper method to grab a token so we don't duplicate login logic everywhere.
    /// </summary>
    private async Task<string> GetTokenAsync(string username, string password)
    {
        var loginXml = $@"
            <LoginRequest>
                <username>{username}</username>
                <password>{password}</password>
            </LoginRequest>";

        var content = new StringContent(loginXml, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/auth/login", content);
        var responseString = await response.Content.ReadAsStringAsync();

        var doc = XDocument.Parse(responseString);
        return doc.Descendants("accessToken").FirstOrDefault()?.Value ?? "";
    }

    // ==========================================
    // GET CARS TESTS
    // ==========================================

    [Fact]
    public async Task GetCars_WithoutToken_Returns401Unauthorized()
    {
        // Act - Requesting without adding a Bearer token
        var response = await _client.GetAsync("/api/v1/Cars");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCars_WithValidToken_Returns200OkAndXml()
    {
        // Arrange
        var token = await GetTokenAsync("admin", "ManagerPass123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/Cars");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");
    }

    // ==========================================
    // ADD CAR TESTS
    // ==========================================

    [Fact]
    public async Task AddCar_WithAdminToken_Returns201Created()
    {
        // Arrange
        var token = await GetTokenAsync("admin", "ManagerPass123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // We use a random VIN so the test doesn't fail on a "duplicate VIN" error if run twice
        var randomVin = "TESTVIN" + DateTime.Now.Ticks.ToString().Substring(0, 10);

        // FIXED: Changed "Gasoline" to "petrol" to satisfy the XSD Enum constraint
        var carXml = $@"
            <Car>
                <id>{randomVin}</id>
                <brand>Toyota</brand>
                <model>Camry</model>
                <year>2023</year>
                <price>
                    <currency>USD</currency>
                    <value>28000</value>
                </price>
                <engine> 
                    <type>petrol</type>
                </engine>
                <horsepower>203</horsepower>
            </Car>";

        var content = new StringContent(carXml, Encoding.UTF8, "application/xml");

        // Act
        var response = await _client.PostAsync("/api/v1/Cars", content);

        // Assert
        var errorMessage = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            because: $"the API should accept the car, but it returned a Bad Request: {errorMessage}");
    }

    // ==========================================
    // DELETE CAR TESTS
    // ==========================================

    [Fact]
    public async Task DeleteCar_WithAdminTokenForFakeVin_Returns404NotFound()
    {
        // Arrange
        var token = await GetTokenAsync("admin", "ManagerPass123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync("/api/v1/Cars/FAKEVIN9999999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}