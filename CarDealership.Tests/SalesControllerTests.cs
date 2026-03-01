using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CarDealershipApi.Tests;

public class SalesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SalesControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetTokenAsync(string username, string password)
    {
        var loginXml = $"<LoginRequest><username>{username}</username><password>{password}</password></LoginRequest>";
        var content = new StringContent(loginXml, Encoding.UTF8, "application/xml");
        var response = await _client.PostAsync("/api/v1/auth/login", content);
        if (!response.IsSuccessStatusCode) return "";
        var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.Descendants("accessToken").FirstOrDefault()?.Value ?? "";
    }

    [Fact]
    public async Task SalesWorkflow_FullLifecycleTest()
    {
        // 1. LOGIN AS ADMIN
        var adminToken = await GetTokenAsync("admin", "ManagerPass123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 2. REGISTER THE SALESPERSON (jdoe)
        // Since we are running tests, we use a unique name or ignore 409 Conflict if already exists
        var registerXml = "<RegisterRequest><username>jdoe</username><password>SalesPass123!</password></RegisterRequest>";
        var regResponse = await _client.PostAsync("/api/v1/auth/register", new StringContent(registerXml, Encoding.UTF8, "application/xml"));
        // We accept 201 (Created) or 409 (Already exists from previous test run)
        regResponse.StatusCode.Should().Match(s => s == HttpStatusCode.Created || s == HttpStatusCode.Conflict);

        // 3. CREATE A CAR (ADMIN ONLY)
        var vin = "SALEVIN" + DateTime.Now.Ticks.ToString().Substring(0, 10); // 17 characters for XSD
        var carXml = $@"
            <Car>
                <id>{vin}</id>
                <brand>Tesla</brand>
                <model>Model 3</model>
                <year>2024</year>
                <price><currency>USD</currency><value>45000</value></price>
                <engine><type>electric</type></engine>
                <horsepower>283</horsepower>
            </Car>";

        var createResponse = await _client.PostAsync("/api/v1/Cars", new StringContent(carXml, Encoding.UTF8, "application/xml"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. TRY TO SELL AS ADMIN (SHOULD BE 403 FORBIDDEN)
        var saleRequestXml = $@"
            <SaleRequest>
                <carId>{vin}</carId>
                <customerId>CUST001</customerId>
                <price>44000</price>
                <paymentMethod>Cash</paymentMethod>
            </SaleRequest>";

        var sellAsAdminResponse = await _client.PostAsync("/api/v1/Sales/sell", new StringContent(saleRequestXml, Encoding.UTF8, "application/xml"));
        sellAsAdminResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Managers cannot sell cars; only Salespeople.");

        // 5. LOGIN AS SALESPERSON (jdoe)
        var salesToken = await GetTokenAsync("jdoe", "SalesPass123!");
        salesToken.Should().NotBeNullOrEmpty();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", salesToken);

        // 6. SELL THE CAR AS SALESPERSON
        var sellAsSalesResponse = await _client.PostAsync("/api/v1/Sales/sell", new StringContent(saleRequestXml, Encoding.UTF8, "application/xml"));
        sellAsSalesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var saleResponseXml = await sellAsSalesResponse.Content.ReadAsStringAsync();
        var saleId = XDocument.Parse(saleResponseXml).Descendants("saleId").First().Value;

        // 7. TRY TO REVERT AS ADMIN (SHOULD BE 403 FORBIDDEN)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var revertAsAdminResponse = await _client.PostAsync($"/api/v1/Sales/revert/{saleId}", null);
        revertAsAdminResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 8. REVERT AS SALESPERSON (SHOULD BE 200 OK)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", salesToken);
        var revertAsSalesResponse = await _client.PostAsync($"/api/v1/Sales/revert/{saleId}", null);
        revertAsSalesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}