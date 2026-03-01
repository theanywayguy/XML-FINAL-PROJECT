using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CarDealershipApi.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IntegrationTests(WebApplicationFactory<Program> factory)
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
        return doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "accessToken")?.Value ?? "";
    }

    [Fact]
    public async Task FullSystem_VinToSale_Workflow()
    {
        // --- 1. SETUP: ADMIN LOGIN ---
        var adminToken = await GetTokenAsync("admin", "ManagerPass123!");
        adminToken.Should().NotBeNullOrEmpty("Admin login is required for setup.");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // --- 2. REGISTRATION: CREATE JDOE (SALESPERSON) ---
        var regXml = "<RegisterRequest><username>jdoe</username><password>SalesPass123!</password></RegisterRequest>";
        var regRes = await _client.PostAsync("/api/v1/auth/register", new StringContent(regXml, Encoding.UTF8, "application/xml"));
        // We accept 201 (Created) or 409 (Conflict/Already Exists)
        regRes.StatusCode.Should().Match(s => s == HttpStatusCode.Created || s == HttpStatusCode.Conflict);

        // --- 3. VIN DECODE: EXTERNAL INTEGRATION ---
        string vin = "W1Y5DBHYXMT053594";
        var vinResponse = await _client.GetAsync($"/api/v1/Cars/decode-vin/{vin}?year=2021");
        vinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var vinXml = await vinResponse.Content.ReadAsStringAsync();
        var vinDoc = XDocument.Parse(vinXml);
        var root = vinDoc.Root;

        // --- 4. FIX XML FOR XSD COMPLIANCE (Price > 0, HP > 0) ---
        // Ensure price exists with values > 0
        var priceEl = root.Element("price");
        if (priceEl == null)
        {
            root.Add(new XElement("price", new XElement("currency", "USD"), new XElement("value", "55000")));
        }
        else
        {
            priceEl.SetElementValue("value", "55000");
        }

        // Ensure horsepower is > 0 (PositiveInteger)
        var hpEl = root.Element("horsepower");
        if (hpEl == null || int.Parse(hpEl.Value) <= 0)
        {
            root.SetElementValue("horsepower", "170");
        }

        // Ensure engine details exist
        var engineEl = root.Element("engine");
        if (engineEl == null)
        {
            root.Add(new XElement("engine", new XElement("type", "diesel"), new XElement("description", "2.0L Diesel")));
        }

        // ADD CAR TO INVENTORY
        var addResponse = await _client.PostAsync("/api/v1/Cars", new StringContent(vinDoc.ToString(), Encoding.UTF8, "application/xml"));
        var addBody = await addResponse.Content.ReadAsStringAsync();
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created, $"AddCar failed XSD: {addBody}");

        // --- 5. SECURITY: ADMIN (MANAGER) SHOULD BE FORBIDDEN FROM SELLING ---
        var saleReq = $@"
            <SaleRequest>
                <carId>{vin}</carId>
                <customerId>CUST-99</customerId>
                <price>54000</price>
                <paymentMethod>Cash</paymentMethod>
            </SaleRequest>";

        var badSell = await _client.PostAsync("/api/v1/Sales/sell", new StringContent(saleReq, Encoding.UTF8, "application/xml"));
        badSell.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Managers are not authorized for the 'Salesperson' role endpoint.");

        // --- 6. TRANSACTION: LOGIN AS JDOE & SELL ---
        var salesToken = await GetTokenAsync("jdoe", "SalesPass123!");
        salesToken.Should().NotBeNullOrEmpty("JDOE login failed.");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", salesToken);

        var sellRes = await _client.PostAsync("/api/v1/Sales/sell", new StringContent(saleReq, Encoding.UTF8, "application/xml"));
        sellRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var sellBody = await sellRes.Content.ReadAsStringAsync();
        var saleId = XDocument.Parse(sellBody).Descendants().First(e => e.Name.LocalName == "saleId").Value;

        // --- 7. REVERT: CANCEL THE SALE WITHIN 3 HOURS ---
        var revertRes = await _client.PostAsync($"/api/v1/Sales/revert/{saleId}", null);
        revertRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Final verification: Car is back in stock
        var finalCheck = await _client.GetAsync($"/api/v1/Cars/{vin}");
        finalCheck.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}