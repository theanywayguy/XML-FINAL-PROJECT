using CarDealershipApi.Middleware;
using CarDealershipApi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// STRICT REQUIREMENT: Enable AddXmlSerializerFormatters()
builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true; // Force client to accept XML
})
.AddXmlSerializerFormatters();

// Swagger Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Car Dealership XML API",
        Version = "v1",
        Description = "A strict XML-based REST API for a Car Dealership."
    });

    // Configure Swagger UI to accept our custom API Key Header
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key authentication. Enter your API Key below.\nExample: SecretDealershipKey123",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                },
                Scheme = "oauth2",
                Name = "ApiKey",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

// Register Custom Services
builder.Services.AddScoped<XmlDataService>();
builder.Services.AddScoped<XsdValidationService>();
builder.Services.AddScoped<XsltTransformationService>();
builder.Services.AddHttpClient<VinIntegrationService>();

var app = builder.Build();

// Enable Swagger UI (We enable it globally here for showcasing purposes)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Car Dealership XML API v1");
    // This serves Swagger UI at the application's root (e.g., https://localhost:7001/)
    c.RoutePrefix = string.Empty;
});

//app.UseHttpsRedirection();

// Use Custom API Key Middleware
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();