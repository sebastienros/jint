using Jint.Runtime.Modules;

namespace Jint.Tests.Runtime.Modules;

public class DefaultModuleLoaderTests
{
    [Theory]
    [InlineData("./other.js", "file:///project/folder/other.js")]
    [InlineData("../model/other.js", "file:///project/model/other.js")]
    [InlineData("/project/model/other.js", "file:///project/model/other.js")]
    [InlineData("file:///project/model/other.js", "file:///project/model/other.js")]
    public void ShouldResolveRelativePaths(string specifier, string expectedUri)
    {
        var resolver = new DefaultModuleLoader("file:///project");

        var resolved = resolver.Resolve("file:///project/folder/script.js", new ModuleRequest(specifier, []));

        Assert.Equal(specifier, resolved.ModuleRequest.Specifier);
        Assert.Equal(expectedUri, resolved.Key);
        Assert.Equal(expectedUri, resolved.Uri?.AbsoluteUri);
        Assert.Equal(SpecifierType.RelativeOrAbsolute, resolved.Type);
    }

    [Theory]
    [InlineData("./../../other.js")]
    [InlineData("../../model/other.js")]
    [InlineData("/model/other.js")]
    [InlineData("file:///etc/secret.js")]
    public void ShouldRejectPathsOutsideOfBasePath(string specifier)
    {
        var resolver = new DefaultModuleLoader("file:///project");

        var exc = Assert.Throws<ModuleResolutionException>(() => resolver.Resolve("file:///project/folder/script.js", new ModuleRequest(specifier, [])));
        Assert.StartsWith(exc.ResolverAlgorithmError, "Unauthorized Module Path");
        Assert.StartsWith(exc.Specifier, specifier);
    }

    [Fact]
    public void ShouldResolveBareSpecifiers()
    {
        var resolver = new DefaultModuleLoader("/");

        var resolved = resolver.Resolve(null, new ModuleRequest("my-module", []));

        Assert.Equal("my-module", resolved.ModuleRequest.Specifier);
        Assert.Equal("my-module", resolved.Key);
        Assert.Equal(null, resolved.Uri?.AbsoluteUri);
        Assert.Equal(SpecifierType.Bare, resolved.Type);
    }
}
