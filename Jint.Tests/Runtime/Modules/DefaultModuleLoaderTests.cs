using Jint.Runtime.Modules;

namespace Jint.Tests.Runtime.Modules;

public class DefaultModuleLoaderTests
{
    [TestCase("./other.js", "file:///project/folder/other.js")]
    [TestCase("../model/other.js", "file:///project/model/other.js")]
    [TestCase("/project/model/other.js", "file:///project/model/other.js")]
    [TestCase("file:///project/model/other.js", "file:///project/model/other.js")]
    public void ShouldResolveRelativePaths(string specifier, string expectedUri)
    {
        var resolver = new DefaultModuleLoader("file:///project");

        var resolved = resolver.Resolve("file:///project/folder/script.js", new ModuleRequest(specifier, []));

        resolved.ModuleRequest.Specifier.Should().Be(specifier);
        resolved.Key.Should().Be(expectedUri);
        (resolved.Uri?.AbsoluteUri).Should().Be(expectedUri);
        resolved.Type.Should().Be(SpecifierType.RelativeOrAbsolute);
    }

    [TestCase("./../../other.js")]
    [TestCase("../../model/other.js")]
    [TestCase("/model/other.js")]
    [TestCase("file:///etc/secret.js")]
    public void ShouldRejectPathsOutsideOfBasePath(string specifier)
    {
        var resolver = new DefaultModuleLoader("file:///project");

        var exc = Invoking(() => resolver.Resolve("file:///project/folder/script.js", new ModuleRequest(specifier, []))).Should().ThrowExactly<ModuleResolutionException>().Which;
        "Unauthorized Module Path".Should().StartWith(exc.ResolverAlgorithmError);
        specifier.Should().StartWith(exc.Specifier);
    }

    [Test]
    public void ShouldResolveBareSpecifiers()
    {
        var resolver = new DefaultModuleLoader("/");

        var resolved = resolver.Resolve(null, new ModuleRequest("my-module", []));

        resolved.ModuleRequest.Specifier.Should().Be("my-module");
        resolved.Key.Should().Be("my-module");
        (resolved.Uri?.AbsoluteUri).Should().BeNull();
        resolved.Type.Should().Be(SpecifierType.Bare);
    }
}
