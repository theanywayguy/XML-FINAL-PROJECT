using System.Xml;
using System.Xml.Xsl;

namespace CarDealershipApi.Services;

public class XsltTransformationService
{
    private readonly string _xmlFilePath;
    private readonly string _xsltFilePath;
    private readonly XmlUrlResolver _resolver;

    public XsltTransformationService(IWebHostEnvironment env)
    {
        _xmlFilePath = Path.Combine(env.ContentRootPath, "Data", "dealership.xml");
        _xsltFilePath = Path.Combine(env.ContentRootPath, "Data", "transform.xslt");

        // Required to allow document() inside XSLT
        _resolver = new XmlUrlResolver
        {
            Credentials = System.Net.CredentialCache.DefaultCredentials
        };
    }

    public string GenerateHtmlReport()
    {
        var settings = new XsltSettings(enableDocumentFunction: true, enableScript: false);

        var transform = new XslCompiledTransform();
        transform.Load(_xsltFilePath, settings, _resolver);

        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit
        };

        using var xmlReader = XmlReader.Create(_xmlFilePath, readerSettings);
        using var stringWriter = new StringWriter();

        var writerSettings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = true
        };

        using var xmlWriter = XmlWriter.Create(stringWriter, writerSettings);

        transform.Transform(xmlReader, null, xmlWriter, _resolver);

        xmlWriter.Flush();

        return stringWriter.ToString();
    }
}