using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities.Extraction;

namespace CompareHub.Backend.Tests;

public class UnitTest1
{
    [Fact]
    public void CleanText_RemovesHtmlEntitiesAndExtraWhitespace()
    {
        var result = ProductExtractionNormalizer.CleanText(" <b> Hello&nbsp;World </b> ");

        Assert.Equal("Hello World", result);
    }
}
