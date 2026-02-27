using System.Xml.Xsl;

namespace CarDealershipApi.Services;

public class XsltTransformationService
{
    private readonly string _xmlFilePath;
    private readonly string _xsltFilePath;

    public XsltTransformationService(IWebHostEnvironment env)
    {
        _xmlFilePath = Path.Combine(env.ContentRootPath, "Data", "dealership.xml");
        _xsltFilePath = Path.Combine(env.ContentRootPath, "Data", "transform.xslt");
    }

    public string GenerateHtmlReport()
    {
        var transform = new XslCompiledTransform();
        transform.Load(_xsltFilePath);

        using var stringWriter = new StringWriter();
        transform.Transform(_xmlFilePath, null, stringWriter);

        return stringWriter.ToString();
    }
}