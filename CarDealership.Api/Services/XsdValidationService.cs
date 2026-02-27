using System.Xml;
using System.Xml.Schema;

namespace CarDealershipApi.Services;

public class XsdValidationService
{
    private readonly string _dealershipSchemaPath;
    private readonly string _dealershipXmlPath;

    public XsdValidationService(IWebHostEnvironment env)
    {
        _dealershipSchemaPath = Path.Combine(env.ContentRootPath, "Data", "schema.xsd");
        _dealershipXmlPath = Path.Combine(env.ContentRootPath, "Data", "dealership.xml");
    }

    public List<string> ValidateDealershipXml()
    {
        return ValidateXmlFile(_dealershipXmlPath, _dealershipSchemaPath);
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