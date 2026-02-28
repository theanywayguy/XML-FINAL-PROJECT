<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="UTF-8" indent="yes"/>

  <xsl:variable name="salesDoc" select="document('sales.xml')" />
  <xsl:variable name="usersDoc" select="document('users.xml')" />
  <xsl:variable name="mainDoc" select="/" />

  <xsl:key name="salesBySalesman" match="sale" use="salesmanId" />

  <xsl:template match="/">
    <html>
      <head>
        <title>Dealership Executive Report</title>
        <style>
          :root { --primary: #2c3e50; --secondary: #3498db; --success: #2ecc71; --dark: #1a252f; --bg: #f8f9fa; }
          body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; background-color: var(--bg); color: #333; }
          .header { background-color: var(--primary); color: white; padding: 20px 40px; display: flex; justify-content: space-between; align-items: center; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }
          .header h1 { margin: 0; font-size: 24px; font-weight: 500; }
          .header .date { font-size: 14px; opacity: 0.8; }
          .container { padding: 30px 40px; max-width: 1400px; margin: 0 auto; }
          .dashboard { display: flex; gap: 20px; margin-bottom: 40px; }
          .card { background: white; padding: 25px; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.04); flex: 1; border-top: 4px solid var(--secondary); }
          .card.success { border-top-color: var(--success); }
          .card h3 { margin: 0 0 10px 0; color: #7f8c8d; font-size: 13px; text-transform: uppercase; letter-spacing: 1px; }
          .card .value { font-size: 32px; font-weight: bold; color: var(--dark); }
          .card .sub-text { font-size: 13px; color: #95a5a6; margin-top: 5px; }
          h2 { color: var(--primary); border-bottom: 2px solid var(--secondary); padding-bottom: 8px; margin-top: 40px; display: inline-block; font-size: 20px; }
          table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.04); margin-bottom: 20px; }
          th, td { padding: 15px 20px; text-align: left; border-bottom: 1px solid #ecf0f1; }
          th { background-color: white; color: var(--primary); font-weight: 600; text-transform: uppercase; font-size: 12px; border-bottom: 2px solid #ecf0f1; }
          tr:last-child td { border-bottom: none; }
          tr:hover { background-color: #fcfcfc; }
          .badge { padding: 5px 10px; border-radius: 20px; font-size: 11px; font-weight: bold; text-transform: uppercase; }
          .badge.electric { background: #e0f7fa; color: #00838f; }
          .badge.hybrid { background: #e8f5e9; color: #2e7d32; }
          .badge.gas { background: #fff3e0; color: #ef6c00; }
        </style>
      </head>
      <body>
        <div class="header">
          <h1>Dealership Executive Report</h1>
          <div class="date" id="reportDate">Loading date...</div>
        </div>

        <div class="container">
          <div class="dashboard">
            <div class="card">
              <h3>Total Cars in Inventory</h3>
              <div class="value"><xsl:value-of select="count(dealership/cars/car)"/></div>
            </div>
            <div class="card success">
              <h3>Total Lifetime Sales</h3>
              <div class="value"><xsl:value-of select="count($salesDoc//sale)"/></div>
            </div>
            <div class="card">
              <h3>Top Salesperson</h3>
              <div class="value">
                <xsl:for-each select="$salesDoc">
                  <xsl:for-each select="sales/sale[generate-id() = generate-id(key('salesBySalesman', salesmanId)[1])]">
                    <xsl:sort select="count(key('salesBySalesman', salesmanId))" data-type="number" order="descending"/>
                    <xsl:if test="position() = 1">
                      <xsl:variable name="winnerId" select="salesmanId" />
                      <xsl:variable name="winnerName" select="$usersDoc//user[id=$winnerId]/username" />
                      <xsl:choose>
                        <xsl:when test="string-length($winnerName) &gt; 0">
                          <xsl:value-of select="$winnerName"/>
                        </xsl:when>
                        <xsl:otherwise>
                          ID: <xsl:value-of select="substring($winnerId, 1, 8)"/>...
                        </xsl:otherwise>
                      </xsl:choose>
                    </xsl:if>
                  </xsl:for-each>
                </xsl:for-each>
              </div>
              <div class="sub-text">Based on total volume</div>
            </div>
          </div>

          <h2>Recent Sales Transactions</h2>
          <table>
            <tr>
              <th>Date</th>
              <th>Salesperson</th>
              <th>Customer</th>
              <th>Vehicle Sold</th>
              <th>Method</th>
              <th>Price</th>
            </tr>
            <xsl:for-each select="$salesDoc//sale">
              <xsl:sort select="dateTime" order="descending"/>
              <tr>
                <td><xsl:value-of select="substring(dateTime, 1, 10)"/></td>
                <td>
                  <xsl:variable name="sId" select="salesmanId" />
                  <xsl:variable name="sName" select="$usersDoc//user[id=$sId]/username" />
                  <xsl:choose>
                    <xsl:when test="string-length($sName) &gt; 0"><b><xsl:value-of select="$sName"/></b></xsl:when>
                    <xsl:otherwise><xsl:value-of select="substring($sId, 1, 8)"/></xsl:otherwise>
                  </xsl:choose>
                </td>
                <td>
                  <xsl:variable name="cId" select="customerId" />
                  <xsl:variable name="cName" select="$mainDoc//customer[@id=$cId]/name" />
                  <xsl:choose>
                    <xsl:when test="string-length($cName) &gt; 0"><xsl:value-of select="$cName"/></xsl:when>
                    <xsl:otherwise><xsl:value-of select="$cId"/></xsl:otherwise>
                  </xsl:choose>
                </td>
                <td><xsl:value-of select="car/brand"/> <xsl:text> </xsl:text> <xsl:value-of select="car/model"/> (<xsl:value-of select="car/year"/>)</td>
                <td><xsl:value-of select="paymentMethod"/></td>
                <td>$<xsl:value-of select="price"/></td>
              </tr>
            </xsl:for-each>
          </table>

          <h2>Available Inventory</h2>
          <table>
            <tr>
              <th>ID</th>
              <th>Brand &amp; Model</th>
              <th>Year</th>
              <th>Engine</th>
              <th>Power</th>
              <th>Current Price</th>
            </tr>
            <xsl:for-each select="dealership/cars/car">
              <xsl:sort select="year" data-type="number" order="descending"/>
              <tr>
                <td><xsl:value-of select="@id"/></td>
                <td><b><xsl:value-of select="brand"/></b> <xsl:text> </xsl:text> <xsl:value-of select="model"/></td>
                <td><xsl:value-of select="year"/></td>
                <td>
                  <xsl:choose>
                    <xsl:when test="engine/@type = 'electric'">
                      <span class="badge electric">Electric</span>
                    </xsl:when>
                    <xsl:when test="engine/@type = 'hybrid'">
                      <span class="badge hybrid">Hybrid</span>
                    </xsl:when>
                    <xsl:otherwise>
                      <span class="badge gas">Gasoline</span>
                    </xsl:otherwise>
                  </xsl:choose>
                </td>
                <td><xsl:value-of select="horsepower"/> hp</td>
                <td><xsl:value-of select="price/@currency"/> <xsl:text> </xsl:text> <xsl:value-of select="price"/></td>
              </tr>
            </xsl:for-each>
          </table>
        </div>

        <script>
          document.getElementById('reportDate').innerText = "Report generated on: " + new Date().toLocaleString();
        </script>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>