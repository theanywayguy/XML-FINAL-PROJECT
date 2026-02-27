<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="html" encoding="UTF-8" indent="yes"/>

  <xsl:template match="/">
    <html>
      <head>
        <title>Dealership Inventory Report</title>
        <style>
          body { font-family: Arial, sans-serif; margin: 20px; }
          table { width: 100%; border-collapse: collapse; margin-top: 20px; }
          th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
          th { background-color: #f2f2f2; }
          .electric { background-color: #e6ffe6; }
          .hybrid { background-color: #e6f2ff; }
        </style>
      </head>
      <body>
        <h2>Available Car Inventory</h2>
        <table>
          <tr>
            <th>ID</th>
            <th>Brand</th>
            <th>Model</th>
            <th>Year</th>
            <th>Engine Type</th>
            <th>Horsepower</th>
            <th>Price</th>
          </tr>
          <xsl:for-each select="dealership/cars/car">
            <xsl:sort select="year" data-type="number" order="descending"/>
            <tr>
              <xsl:choose>
                <xsl:when test="engine/@type = 'electric'">
                  <xsl:attribute name="class">electric</xsl:attribute>
                </xsl:when>
                <xsl:when test="engine/@type = 'hybrid'">
                  <xsl:attribute name="class">hybrid</xsl:attribute>
                </xsl:when>
              </xsl:choose>
              <td><xsl:value-of select="@id"/></td>
              <td><xsl:value-of select="brand"/></td>
              <td><xsl:value-of select="model"/></td>
              <td><xsl:value-of select="year"/></td>
              <td><xsl:value-of select="engine/@type"/></td>
              <td><xsl:value-of select="horsepower"/> hp</td>
              <td><xsl:value-of select="price/@currency"/> <xsl:text> </xsl:text> <xsl:value-of select="price"/></td>
            </tr>
          </xsl:for-each>
        </table>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>