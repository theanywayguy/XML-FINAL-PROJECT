# 🚗 Car Dealership XML API

[![GitHub](https://img.shields.io/badge/GitHub-theanywayguy%2FXML--FINAL--PROJECT-181717?style=flat&logo=github)](https://github.com/theanywayguy/XML-FINAL-PROJECT)
[![Docker Hub](https://img.shields.io/badge/Docker%20Hub-theanywayguy%2Fcardealership--api-2496ED?style=flat&logo=docker)](https://hub.docker.com/r/theanywayguy/cardealership-api)

A fully-featured REST API built with **ASP.NET Core** that demonstrates a complete XML ecosystem — from schema design and XSLT transformation to JWT-secured endpoints and real-world external service integration. All data is stored, validated, and transmitted as **XML**.

-----

## 📋 Table of Contents

- [Project Overview](#project-overview)
- [Tech Stack](#tech-stack)
- [Architecture & Folder Structure](#architecture--folder-structure)
- [XML Data Model](#xml-data-model)
- [XSD Schema Validation](#xsd-schema-validation)
- [XPath Queries](#xpath-queries)
- [XSLT Transformation](#xslt-transformation)
- [External API Integration (NHTSA VIN Decoder)](#external-api-integration-nhtsa-vin-decoder)
- [Getting Started](#getting-started)
  - [Option A: Local .NET](#option-a-local-net)
  - [Option B: Docker](#option-b-docker)
- [Authentication](#authentication)
- [API Reference](#api-reference)
  - [Auth Endpoints](#auth-endpoints)
  - [Cars Endpoints](#cars-endpoints)
  - [Sales Endpoints](#sales-endpoints)
  - [Reports Endpoint](#reports-endpoint)
- [Error Handling](#error-handling)
- [Security Features](#security-features)
- [Requirements Coverage](#requirements-coverage)

-----

## Project Overview

**AutoAxis** is a car dealership management API that uses XML as its primary data format for everything: inventory storage, sale records, user accounts, API request/response bodies, and inter-service data exchange.

Core capabilities demonstrated:

|Capability          |Implementation                                                              |
|--------------------|----------------------------------------------------------------------------|
|XML Design          |`dealership.xml`, `sales.xml`, `users.xml` with nested, attributed structure|
|XSD Validation      |`schema.xsd`, `sales.xsd`, `vin-schema.xsd` — validated on every write      |
|XPath Queries       |Dynamic query builder in `XmlDataService` + 5 named query methods           |
|XSLT Transformation |`transform.xslt` → HTML Executive Report (cross-document join)              |
|XML Parsing (DOM)   |LINQ to XML (`XDocument`) throughout `XmlDataService`                       |
|REST API with XML   |11 endpoints across 4 versioned controllers                                 |
|External Integration|NHTSA VIN Decoder API (JSON → XML → XSD validated)                          |
|JWT Auth            |Role-based access (Manager / Salesperson)                                   |
|API Documentation   |Swagger/OpenAPI at `/swagger`                                               |

-----

## Tech Stack

- **Runtime:** .NET 8 / ASP.NET Core
- **XML Processing:** `System.Xml`, `System.Xml.Linq`, `System.Xml.XPath`, `System.Xml.Xsl`
- **Authentication:** JWT Bearer Tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Documentation:** Swagger / OpenAPI (`Swashbuckle`)
- **Logging:** Serilog (console + rolling file)
- **Containerisation:** Docker + Docker Compose
- **External API:** [NHTSA vPIC REST API](https://vpic.nhtsa.dot.gov/api/)

-----

## Architecture & Folder Structure

```
XML-FINAL-PROJECT-main/
├── CarDealership.Api/
│   ├── Controllers/
│   │   └── v1/
│   │       ├── AuthController.cs        # Login & Register
│   │       ├── CarsContoller.cs         # Full car CRUD + search + VIN decode
│   │       ├── SaleController.cs        # Process & revert sales
│   │       └── ReportController.cs      # XSLT HTML report
│   ├── Data/
│   │   ├── dealership.xml               # Main inventory dataset
│   │   ├── sales.xml                    # Historical sale records
│   │   ├── users.xml                    # User accounts (hashed passwords)
│   │   ├── schema.xsd                   # Inventory XSD schema
│   │   ├── sales.xsd                    # Sales XSD schema
│   │   ├── vin-schema.xsd               # External VIN response XSD
│   │   ├── invalid-dealership.xml       # Intentionally invalid XML (demo)
│   │   └── transform.xslt               # HTML report transformation
│   ├── Middleware/
│   │   ├── GlobalExceptionMiddleware.cs # Catch-all 500 XML error handler
│   │   └── XmlExceptionMiddleware.cs
│   ├── Models/
│   │   ├── Car.cs                       # Car, Price, Engine models
│   │   ├── Sales.cs                     # Sale, SaleRequest, SaleResponse
│   │   ├── AuthModels.cs                # LoginRequest, RegisterRequest, TokenResponse
│   │   └── XmlError.Cs                  # Consistent XML error response model
│   ├── Services/
│   │   ├── XmlDataService.cs            # DOM parsing, CRUD, XPath queries
│   │   ├── XsdValidationService.cs      # Schema validation (file + string)
│   │   ├── XsltTransformationService.cs # XSLT → HTML
│   │   ├── VinIntegrationService.cs     # NHTSA API integration
│   │   └── UsersServices.cs             # Auth + user management
│   └── Program.cs                       # DI, middleware pipeline, JWT, rate limiting
├── Dockerfile
├── docker-compose.yml
└── README.md
```

-----

## XML Data Model

### `dealership.xml` — Main Inventory

The primary dataset. 24 cars with full nested structure and typed attributes.

```xml
<?xml version="1.0" encoding="utf-8"?>
<dealership>
  <cars>
    <car id="c16">
      <brand>Porsche</brand>
      <model>Taycan</model>
      <year>2023</year>
      <price currency="USD">85000</price>
      <engine type="electric">Dual Motor</engine>
      <horsepower>402</horsepower>
    </car>
    <!-- 23 more cars... -->
  </cars>
</dealership>
```

**Data model highlights:**

- `id` attribute on `<car>` is typed as `xs:ID` (globally unique, enforced by XSD)
- `currency` attribute on `<price>` carries the ISO currency code
- `type` attribute on `<engine>` is constrained by an XSD enum (`petrol`, `diesel`, `hybrid`, `electric`)
- camelCase naming convention used throughout

### `sales.xml` — Transaction Log

Each sale archives the full car snapshot inside the `<sale>` element, enabling full reversion without data loss.

```xml
<sales>
  <sale id="387fd8c8-3420-4672-8663-f04f2b98ab17">
    <salesmanId>9c7a081d-0b79-4281-aa3c-17c9483b93d7</salesmanId>
    <customerId>c1</customerId>
    <carId>c5</carId>
    <dateTime>2026-02-28T12:36:28.1369580Z</dateTime>
    <paymentMethod>Financed</paymentMethod>
    <price>35000.00</price>
    <car id="c5"> <!-- Full car snapshot archived here -->
      <brand>Toyota</brand>
      <model>Prius</model>
      ...
    </car>
  </sale>
</sales>
```

-----

## XSD Schema Validation

Three XSD schemas cover all XML documents in the system. Validation runs automatically on **every write operation** — if a change would break the schema, it is rolled back.

### `schema.xsd` — Inventory Schema

Demonstrates advanced XSD features:

```xml
<!-- Custom type: price must be > 0 -->
<xs:simpleType name="positiveDecimal">
  <xs:restriction base="xs:decimal">
    <xs:minExclusive value="0"/>
  </xs:restriction>
</xs:simpleType>

<!-- Enum: only these engine types are valid -->
<xs:simpleType name="engineTypeEnum">
  <xs:restriction base="xs:string">
    <xs:enumeration value="petrol"/>
    <xs:enumeration value="diesel"/>
    <xs:enumeration value="hybrid"/>
    <xs:enumeration value="electric"/>
  </xs:restriction>
</xs:simpleType>

<!-- Car ID typed as xs:ID — enforces global uniqueness -->
<xs:attribute name="id" type="xs:ID" use="required"/>
```

### `invalid-dealership.xml` — Intentional Failure Demo

This file is provided to demonstrate what schema validation catches:

```xml
<car> <!-- Missing required 'id' attribute -->
  <price currency="USD">-5000</price> <!-- Fails: price must be > 0 -->
  <engine type="nuclear">...</engine> <!-- Fails: 'nuclear' not in enum -->
</car>
```

**Validation failures caught:**

1. Missing `id` attribute (required by schema)
1. Negative price value (violates `positiveDecimal` restriction)
1. Invalid engine type `"nuclear"` (not in the `engineTypeEnum` enumeration)

### Validation in Code

```csharp
// XsdValidationService.cs — validates against schema on every mutation
public List<string> ValidateDealershipXml()
{
    var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
    settings.Schemas.Add(null, _dealershipSchemaPath);
    settings.ValidationEventHandler += (s, e) => errors.Add($"[{e.Severity}] {e.Message}");
    using var reader = XmlReader.Create(_dealershipXmlPath, settings);
    while (reader.Read()) { }
    return errors;
}
```

If validation errors are returned after a `POST` or `PUT`, the change is automatically rolled back and a `400` error is returned to the client.

-----

## XPath Queries

XPath is used in two ways: as **named query methods** for specific business logic, and as a **dynamic query builder** for the search endpoint.

### Named XPath Queries

|#|Method                  |XPath Expression                   |Purpose                          |
|-|------------------------|-----------------------------------|---------------------------------|
|1|`GetCarsAbovePrice(min)`|`//car[price > {min}]`             |Filter cars by minimum price     |
|2|`GetHybridCars()`       |`//car[engine/@type='hybrid']`     |Select all hybrid vehicles       |
|3|`GetCustomerCount()`    |`count(//customer)`                |Count total customers (aggregate)|
|4|`GetSalesPerCustomer()` |`count(//sale[@customerId='{id}'])`|Sales volume per customer        |
|5|`GetCarsNewerThan(year)`|`//car[year > {year}]`             |Filter inventory by model year   |

### Dynamic XPath Builder (Search Endpoint)

The `GET /api/v1/cars/search` endpoint dynamically constructs XPath predicates from query parameters:

```csharp
// Builds: //car[translate(brand,'ABC...','abc...')='toyota' and price >= 20000 and engine/@type='hybrid']
var conditions = new List<string>();
if (brand != null)    conditions.Add($"translate(brand,'{upper}','{lower}')='{brand.ToLower()}'");
if (minPrice != null) conditions.Add($"price >= {minPrice}");
if (isHybrid == true) conditions.Add("engine/@type = 'hybrid'");

string xpath = "//car" + (conditions.Any() ? "[" + string.Join(" and ", conditions) + "]" : "");
var elements = doc.XPathSelectElements(xpath);
```

**Example queries you can run:**

```
GET /api/v1/cars/search?brand=BMW
GET /api/v1/cars/search?minPrice=40000&maxPrice=70000
GET /api/v1/cars/search?isHybrid=true
GET /api/v1/cars/search?brand=Toyota&year=2021
```

-----

## XSLT Transformation

**File:** `Data/transform.xslt`  
**Output:** Styled HTML Executive Report  
**Accessible at:** `GET /report` (Manager role required)

The transformation is a **cross-document join** — it reads three XML files simultaneously (`dealership.xml`, `sales.xml`, `users.xml`) and produces a single unified HTML report.

### What the XSLT does

```xml
<!-- Load all three XML documents -->
<xsl:variable name="salesDoc" select="document('sales.xml')"/>
<xsl:variable name="usersDoc" select="document('users.xml')"/>

<!-- Dashboard card: count all cars in inventory -->
<div class="value"><xsl:value-of select="count(dealership/cars/car)"/></div>

<!-- Find the salesperson with the most sales using Muenchian grouping -->
<xsl:key name="salesBySalesman" match="sale" use="salesmanId"/>
<xsl:for-each select="sales/sale[generate-id() = generate-id(key('salesBySalesman', salesmanId)[1])]">
  <xsl:sort select="count(key('salesBySalesman', salesmanId))" data-type="number" order="descending"/>
</xsl:for-each>

<!-- Join sales → users to resolve salesperson name from ID -->
<xsl:variable name="sName" select="$usersDoc//user[id=$sId]/username"/>

<!-- Sort inventory by year (newest first) -->
<xsl:for-each select="dealership/cars/car">
  <xsl:sort select="year" data-type="number" order="descending"/>
</xsl:for-each>
```

### Report Sections

1. **Dashboard Cards** — Total inventory count, lifetime sales count, top salesperson name
1. **Recent Sales Table** — Sorted by date descending; salesperson and customer names resolved by ID lookup
1. **Inventory Table** — All cars sorted by year, with colour-coded engine type badges (Electric / Hybrid / Gasoline)

-----

## External API Integration (NHTSA VIN Decoder)

**Service:** [NHTSA vPIC API](https://vpic.nhtsa.dot.gov/api/) — a free US government vehicle database  
**Endpoint:** `GET /api/v1/cars/decode-vin/{vin}`

### Integration Flow

```
Client → [VIN] → API → NHTSA REST (JSON) → Parse JSON → Build XML → Validate vs vin-schema.xsd → Return Car template
```

```csharp
// 1. Call external REST API — returns JSON
var url = $"https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValues/{vin}?format=json";
var response = await _httpClient.GetAsync(url);
var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

// 2. Convert JSON response to XML
var xmlDoc = new XDocument(
    new XElement("DecodedVin", new XAttribute("vin", vin),
        new XElement("Make", make),
        new XElement("Model", model),
        new XElement("Year", year),
        new XElement("FuelType", fuelType),
        new XElement("Horsepower", hp)));

// 3. Validate generated XML against vin-schema.xsd
var errors = _validationService.ValidateXmlString(xmlDoc.ToString(), _vinSchemaPath);
if (errors.Any()) throw new InvalidOperationException("XSD validation failed");

// 4. Map to internal Car model and return
return new Car { Id = vin, Brand = make, Model = model, ... };
```

### Example Response

```http
GET /api/v1/cars/decode-vin/1HGBH41JXMN109186
Authorization: Bearer <token>

HTTP/1.1 200 OK
Content-Type: application/xml

<Car>
  <id>1HGBH41JXMN109186</id>
  <brand>Honda</brand>
  <model>Civic</model>
  <year>2021</year>
  <engine><type>petrol</type><description>Gasoline</description></engine>
  <horsepower>158</horsepower>
</Car>
```

The returned car template can be copied directly into a `POST /api/v1/cars` request to add it to inventory.

-----

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) **or** [Docker](https://www.docker.com/)

### Option A: Local .NET

```bash
# 1. Clone the repository
git clone https://github.com/theanywayguy/XML-FINAL-PROJECT.git
cd XML-FINAL-PROJECT

# 2. Trust the development HTTPS certificate (first time only)
dotnet dev-certs https --clean
dotnet dev-certs https --trust

# 3. Run the API
cd CarDealership.Api
dotnet run --launch-profile "https"
```

The API will start at `https://localhost:7079`.

|URL                                   |Description                    |
|--------------------------------------|-------------------------------|
|`https://localhost:7079/swagger`      |Interactive Swagger UI         |
|`https://localhost:7079/api/v1/health`|Health check endpoint          |
|`https://localhost:7079/report`       |XSLT HTML report (Manager only)|

### Option B: Docker Hub (Recommended — no build required)

The pre-built image is published on Docker Hub and is the fastest way to get started.

```bash
# Pull the latest image
docker pull theanywayguy/cardealership-api:latest
```

**Quick start (ephemeral — data resets on container stop):**

```bash
docker run -d \
  -p 5048:8080 \
  --name dealership-demo \
  theanywayguy/cardealership-api:latest
```

**Production start (persistent data volume — recommended):**

```bash
# 1. Create a local folder to store your XML data
mkdir -p ~/my-car-data

# 2. Run with the volume flag so data survives container restarts
docker run -d \
  -p 5048:8080 \
  --name dealership-prod \
  -v ~/my-car-data:/app/Data \
  theanywayguy/cardealership-api:latest
```

Once running, verify the deployment:

|URL                                  |Description   |
|-------------------------------------|--------------|
|`http://localhost:5048`              |Web Dashboard |
|`http://localhost:5048/swagger`      |Swagger API UI|
|`http://localhost:5048/api/v1/health`|Health Status |

### Option C: Docker Compose (build from source)

```bash
# 1. Clone the repository
git clone https://github.com/theanywayguy/XML-FINAL-PROJECT.git
cd XML-FINAL-PROJECT

# 2. Build and start the container
docker-compose up --build

# API available at: http://localhost:5048
# Swagger UI:       http://localhost:5048/swagger
```

> **Note:** The `docker-compose.yml` mounts `./CarDealership.Api/Data` as a volume, so your XML data persists across container restarts.

-----

## Authentication

All endpoints (except `POST /api/v1/auth/login`) require a **JWT Bearer Token**.

### Roles

|Role         |Permissions                                                                           |
|-------------|--------------------------------------------------------------------------------------|
|`Manager`    |Full access — can view/add/update/delete cars, view reports, register new salespersons|
|`Salesperson`|Can view inventory, process sales, revert sales                                       |

### Login Flow

```http
POST /api/v1/auth/login
Content-Type: application/xml
Accept: application/xml

<LoginRequest>
  <username>manager</username>
  <password>your-password</password>
</LoginRequest>
```

```xml
HTTP/1.1 200 OK
Content-Type: application/xml

<TokenResponse>
  <accessToken>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</accessToken>
  <expiresIn>1800</expiresIn>
</TokenResponse>
```

Use the token on all subsequent requests:

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

Tokens expire after **30 minutes**.

-----

## API Reference

All endpoints are versioned under `/api/v1/` and use `application/xml` for both request and response bodies.

### Auth Endpoints

#### `POST /api/v1/auth/login`

Authenticate and receive a JWT token. Open to all — no authentication required.

**Request:**

```xml
<LoginRequest>
  <username>string</username>
  <password>string</password>
</LoginRequest>
```

**Responses:**

|Code |Description                    |
|-----|-------------------------------|
|`200`|Returns JWT access token       |
|`400`|Malformed XML or missing fields|
|`401`|Invalid credentials            |

-----

#### `POST /api/v1/auth/register`

Register a new Salesperson account. **Manager role required.**

**Request:**

```xml
<RegisterRequest>
  <username>new.salesperson</username>
  <password>SecurePass123!</password>
</RegisterRequest>
```

|Code |Description                     |
|-----|--------------------------------|
|`201`|Salesperson created successfully|
|`400`|Missing fields or malformed XML |
|`403`|Caller is not a Manager         |
|`409`|Username already exists         |

-----

### Cars Endpoints

> **Required role:** `Manager` or `Salesperson`

#### `GET /api/v1/cars`

Retrieve all cars currently in inventory.

```http
GET /api/v1/cars
Authorization: Bearer <token>
Accept: application/xml
```

**Response:**

```xml
<ArrayOfCar>
  <Car>
    <id>c16</id>
    <brand>Porsche</brand>
    <model>Taycan</model>
    <year>2023</year>
    <price><currency>USD</currency><value>85000</value></price>
    <engine><type>electric</type><description>Dual Motor</description></engine>
    <horsepower>402</horsepower>
  </Car>
  <!-- more cars... -->
</ArrayOfCar>
```

|Code |Description             |
|-----|------------------------|
|`200`|Returns list of all cars|
|`500`|Internal server error   |

-----

#### `GET /api/v1/cars/{id}`

Retrieve a single car by its ID.

```http
GET /api/v1/cars/c16
Authorization: Bearer <token>
```

|Code |Description    |
|-----|---------------|
|`200`|Returns the car|
|`404`|Car not found  |

-----

#### `GET /api/v1/cars/search`

Search inventory using dynamic XPath with query parameters. All parameters are optional and combinable.

```http
GET /api/v1/cars/search?brand=BMW&minPrice=40000&isHybrid=false
Authorization: Bearer <token>
```

|Parameter |Type   |Example |
|----------|-------|--------|
|`brand`   |string |`Toyota`|
|`model`   |string |`Camry` |
|`year`    |int    |`2022`  |
|`minPrice`|decimal|`25000` |
|`maxPrice`|decimal|`60000` |
|`isHybrid`|bool   |`true`  |

|Code |Description                                     |
|-----|------------------------------------------------|
|`200`|Returns matching cars (empty list if none found)|

-----

#### `POST /api/v1/cars`

Add a new car to inventory. The entire document is validated against `schema.xsd` after insertion — if validation fails, the insertion is automatically rolled back.

```http
POST /api/v1/cars
Authorization: Bearer <token>
Content-Type: application/xml

<Car>
  <id>c99</id>
  <brand>Tesla</brand>
  <model>Model S</model>
  <year>2023</year>
  <price><currency>USD</currency><value>89990</value></price>
  <engine><type>electric</type><description>Dual Motor AWD</description></engine>
  <horsepower>670</horsepower>
</Car>
```

|Code |Description                                           |
|-----|------------------------------------------------------|
|`201`|Car created, returns new car object                   |
|`400`|Invalid XML, missing required fields, or XSD violation|
|`500`|Server error                                          |


> **Role required:** `Manager`

-----

#### `PUT /api/v1/cars/{id}`

Update the price of an existing car. XSD validation runs post-update with automatic rollback on failure.

```http
PUT /api/v1/cars/c16
Authorization: Bearer <token>
Content-Type: application/xml

<PriceUpdate>
  <newPrice>79999</newPrice>
</PriceUpdate>
```

|Code |Description                         |
|-----|------------------------------------|
|`200`|Price updated successfully          |
|`400`|Invalid price (≤ 0) or XSD violation|
|`404`|Car not found                       |


> **Role required:** `Manager`

-----

#### `DELETE /api/v1/cars/{id}`

Remove a car from inventory.

```http
DELETE /api/v1/cars/c99
Authorization: Bearer <token>
```

|Code |Description             |
|-----|------------------------|
|`200`|Car deleted successfully|
|`404`|Car not found           |


> **Role required:** `Manager`

-----

#### `GET /api/v1/cars/decode-vin/{vin}`

Query the external NHTSA API to decode a VIN, convert the JSON response to validated XML, and return a pre-populated car template ready to submit.

```http
GET /api/v1/cars/decode-vin/1HGBH41JXMN109186?year=2021
Authorization: Bearer <token>
```

|Code |Description                               |
|-----|------------------------------------------|
|`200`|Returns populated Car XML template        |
|`400`|VIN response fails internal XSD validation|
|`500`|NHTSA API unreachable                     |

-----

### Sales Endpoints

> **Required role:** `Salesperson`

#### `POST /api/v1/sales/sell`

Process a car sale. This is a **thread-safe atomic transaction** that simultaneously removes the car from `dealership.xml` and writes a full sale record (with archived car snapshot) to `sales.xml`.

```http
POST /api/v1/sales/sell
Authorization: Bearer <token>
Content-Type: application/xml

<SaleRequest>
  <carId>c16</carId>
  <customerId>customer-42</customerId>
  <price>83000</price>
  <paymentMethod>Financed</paymentMethod>
</SaleRequest>
```

Valid `paymentMethod` values: `Cash`, `Credit`, `Financed`

**Response:**

```xml
<SaleResponse>
  <saleId>550e8400-e29b-41d4-a716-446655440000</saleId>
</SaleResponse>
```

|Code |Description                                    |
|-----|-----------------------------------------------|
|`200`|Sale processed, returns the new sale ID        |
|`400`|Missing fields, invalid price, or XSD violation|
|`404`|Car not found in inventory                     |
|`500`|Transactional error                            |

-----

#### `POST /api/v1/sales/revert/{saleId}`

Reverse a sale within 3 hours of the transaction. Returns the archived car to inventory and removes the sale record.

```http
POST /api/v1/sales/revert/550e8400-e29b-41d4-a716-446655440000
Authorization: Bearer <token>
```

|Code |Description                                 |
|-----|--------------------------------------------|
|`200`|Sale reverted, car restored to inventory    |
|`400`|Sale not found, or 3-hour window has expired|
|`500`|Internal error during revert                |

-----

### Reports Endpoint

> **Required role:** `Manager`

#### `GET /report`

Triggers the XSLT transformation and returns a styled HTML Executive Report. The report joins data from all three XML files (inventory, sales, users) in real time.

```http
GET /report
Authorization: Bearer <token>
Accept: text/html
```

Returns a full HTML page with:

- Dashboard summary cards (inventory count, total sales, top salesperson)
- Sales transactions table (date, salesperson name, customer, vehicle, payment method, price)
- Full inventory table (sorted by year descending, with engine type badges)

-----

## Error Handling

All error responses — including auth failures, validation errors, and unhandled exceptions — return a consistent XML format:

```xml
<XmlError>
  <code>404</code>
  <message>Car with id c99 not found.</message>
</XmlError>
```

### Error Scenarios Handled

|Scenario                         |HTTP Code|Handler                                                  |
|---------------------------------|---------|---------------------------------------------------------|
|Malformed or unparseable XML body|`400`    |`XmlExceptionMiddleware`                                 |
|XSD schema validation failure    |`400`    |`XsdValidationService` + controller rollback             |
|Missing required field           |`400`    |Model binding + custom `InvalidModelStateResponseFactory`|
|Resource not found               |`404`    |Controller logic                                         |
|Unauthorized (no/invalid token)  |`401`    |JWT `OnChallenge` event override                         |
|Insufficient role                |`403`    |JWT `OnForbidden` event override                         |
|Duplicate resource               |`409`    |Controller logic                                         |
|Rate limit exceeded              |`429`    |`RateLimiter` middleware                                 |
|Unhandled server exception       |`500`    |`GlobalExceptionMiddleware`                              |

All middleware-level errors (401, 403, 429, 500) are also returned as `application/xml`, ensuring the entire API speaks a consistent XML dialect regardless of where the error originates.

-----

## Security Features

|Feature                     |Details                                                                                               |
|----------------------------|------------------------------------------------------------------------------------------------------|
|**JWT Authentication**      |HMAC-SHA256 signed tokens, 30-minute expiry, zero clock skew                                          |
|**Role-Based Authorization**|`Manager` and `Salesperson` roles enforced per endpoint                                               |
|**Rate Limiting**           |100 requests/minute per IP (fixed window), with 2-request queue buffer                                |
|**HSTS**                    |HTTP Strict Transport Security enabled in Production                                                  |
|**HTTPS Redirection**       |All HTTP traffic redirected to HTTPS                                                                  |
|**Thread-Safe File Access** |`SemaphoreSlim` prevents concurrent XML file corruption during sales                                  |
|**Serilog Audit Logging**   |Every API action logs IP address, username, action, and outcome to console and rolling daily log files|

-----

## Requirements Coverage

|Requirement                 |Status|Notes                                                                                                                                                     |
|----------------------------|------|----------------------------------------------------------------------------------------------------------------------------------------------------------|
|**A — XML Dataset**         |✅     |24 cars in `dealership.xml` + `sales.xml` + `users.xml`; nested structure, attributed elements, camelCase naming                                          |
|**B — XSD Validation**      |✅     |`schema.xsd` (advanced types: enum, positiveDecimal, xs:ID), `sales.xsd`, `vin-schema.xsd`; invalid XML example provided; validation in code with rollback|
|**C — XPath Queries (≥5)**  |✅     |5 named query methods + dynamic multi-predicate builder in `SearchCars`; XSLT uses `count()`, `key()`, `generate-id()`, `document()`                      |
|**C — XSLT Transformation** |✅     |`transform.xslt` → styled HTML; cross-document join across 3 XML files; Muenchian grouping for top salesperson                                            |
|**D — XML Parsing in Code** |✅     |LINQ to XML (`XDocument` / `XElement`) throughout `XmlDataService`; DOM reading, writing, manipulation, malformed XML error handling                      |
|**E — REST API with XML**   |✅     |11 endpoints across 4 controllers; `Content-Type: application/xml` + `Accept: application/xml` enforced; GET/POST/PUT/DELETE all implemented              |
|**F — API Documentation**   |✅     |Swagger/OpenAPI at `/swagger`; XML doc-comments on all controllers/endpoints; JWT auth integrated in Swagger UI                                           |
|**G — External Integration**|✅     |NHTSA vPIC REST API (JSON → XML → XSD validated); fuel type mapped to engine type enum; returns usable Car template                                       |
|**Error Handling**          |✅     |Invalid XML, XSD failure, missing field, 400/401/403/404/409/429/500 — all return consistent XML format                                                   |
|**Reproducibility**         |✅     |Docker Compose one-command setup; persistent data volume; Swagger for interactive testing                                                                 |
|**Optional: JWT Auth**      |✅     |Full role-based access control                                                                                                                            |
|**Optional: API Versioning**|✅     |`/api/v1/` prefix on all routes                                                                                                                           |
|**Optional: Rate Limiting** |✅     |100 req/min per IP with XML error on rejection                                                                                                            |
|**Optional: Advanced XSD**  |✅     |Custom types, enumerations, minExclusive, xs:ID, xs:positiveInteger                                                                                       |
|**Optional: Logging**       |✅     |Serilog with structured logging, IP tracking, rolling daily files                                                                                         |
|**Optional: Filtering**     |✅     |`GET /cars/search` with 6 filterable parameters via dynamic XPath                                                                                         |