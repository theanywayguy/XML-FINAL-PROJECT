using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CarDealershipApi.Tests;

public class ReportsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ReportsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Helper method to grab a token so we don't duplicate login logic.
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

        // If login fails, this returns an empty string, which will appropriately fail the test's auth later
        if (!response.IsSuccessStatusCode) return "";

        var responseString = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(responseString);
        return doc.Descendants("accessToken").FirstOrDefault()?.Value ?? "";
    }

    // ==========================================
    // REPORTS TESTS
    // ==========================================

    [Fact]
    public async Task GetInventoryReport_WithoutToken_Returns401Unauthorized()
    {
        // Act - Requesting the report with no Bearer token attached
        var response = await _client.GetAsync("/report");

        // Assert - The API should completely block the request
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInventoryReport_WithManagerToken_Returns200OkAndHtml()
    {
        // Arrange - Log in as the default admin/Manager
        var token = await GetTokenAsync("admin", "ManagerPass123!");

        // Ensure we actually got a token back
        token.Should().NotBeNullOrEmpty("because the admin user should be able to log in");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Request the report
        var response = await _client.GetAsync("/report");

        // Assert - We should get a 200 OK
        var errorMessage = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"the Manager should have access, but got error: {errorMessage}");

        // Assert - The response content MUST be HTML, not JSON or XML
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        // Assert - Verify the actual content contains basic HTML tags from your XSLT
        var htmlContent = await response.Content.ReadAsStringAsync();
        htmlContent.Should().Contain("<html");
        htmlContent.Should().Contain("Dealership Executive Report");
    }
}