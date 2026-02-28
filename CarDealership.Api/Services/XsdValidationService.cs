using System.Xml;
using System.Xml.Schema;

namespace CarDealershipApi.Services;

public class XsdValidationService
{
    private readonly string _dealershipSchemaPath;
    private readonly string _dealershipXmlPath;

    // Add paths for the sales files
    private readonly string _salesSchemaPath;
    private readonly string _salesXmlPath;

    public XsdValidationService(IWebHostEnvironment env)
    {
        _dealershipSchemaPath = Path.Combine(env.ContentRootPath, "Data", "schema.xsd");
        _dealershipXmlPath = Path.Combine(env.ContentRootPath, "Data", "dealership.xml");

        // Map the new sales files in the Data folder
        _salesSchemaPath = Path.Combine(env.ContentRootPath, "Data", "sales.xsd");
        _salesXmlPath = Path.Combine(env.ContentRootPath, "Data", "sales.xml");
    }

    public List<string> ValidateDealershipXml()
    {
        return ValidateXmlFile(_dealershipXmlPath, _dealershipSchemaPath);
    }

    /// <summary>
    /// Validates the sales.xml file. If it doesn't exist yet, returns no errors.
    /// </summary>
    public List<string> ValidateSalesXml()
    {
        // If the file hasn't been created yet (no sales made), there's nothing to validate!
        if (!File.Exists(_salesXmlPath)) return new List<string>();

        // Reuse your excellent helper method
        return ValidateXmlFile(_salesXmlPath, _salesSchemaPath);
    }

    public List<string> ValidateXmlFile(string xmlFilePath, string schemaFilePath)
    {
        var errors = new List<string>();
        var schemaSet = new XmlSchemaSet();
        schemaSet.Add(null, schemaFilePath);

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemaSet
        };
        settings.ValidationEventHandler += (sender, e) => errors.Add($"[{e.Severity}] {e.Message}");

        using var reader = XmlReader.Create(xmlFilePath, settings);
        while (reader.Read()) { }

        return errors;
    }

    public List<string> ValidateXmlString(string xmlContent, string schemaFilePath)
    {
        var errors = new List<string>();
        var schemaSet = new XmlSchemaSet();
        schemaSet.Add(null, schemaFilePath);

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemaSet
        };
        settings.ValidationEventHandler += (sender, e) => errors.Add($"[{e.Severity}] {e.Message}");

        using var stringReader = new StringReader(xmlContent);
        using var reader = XmlReader.Create(stringReader, settings);
        while (reader.Read()) { }

        return errors;
    }
}