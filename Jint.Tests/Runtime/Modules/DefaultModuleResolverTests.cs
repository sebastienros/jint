using System;
using Jint.Runtime.Modules;
using Xunit;

namespace Jint.Tests.Runtime.Modules
{
    public class DefaultModuleLoaderTests
    {
        [Theory]
        [InlineData("./other.js", @"file://c/project/folder/other.js")]
        [InlineData("../model/other.js", @"file://c/project/model/other.js")]
        [InlineData("/model/other.js", @"file://c/project/model/other.js")]
        public void ShouldResolveRelativePaths(string specifier, string expectedUri)
        {
            var resolver = new TestModuleLoader("file://C/project");

            var resolved = resolver.Resolve("file://C/project/folder/script.js", specifier);

            Assert.Equal(specifier, resolved.Specifier);
            Assert.Equal(expectedUri, resolved.Key);
            Assert.Equal(expectedUri, resolved.Uri?.AbsoluteUri);
            Assert.Equal(SpecifierType.RelativeOrAbsolute, resolved.Type);
        }

        [Fact]
        public void ShouldResolveBareSpecifiers()
        {
            var resolver = new TestModuleLoader("/");

            var resolved = resolver.Resolve(null, "my-module");

            Assert.Equal("my-module", resolved.Specifier);
            Assert.Equal("my-module", resolved.Key);
            Assert.Equal(null, resolved.Uri?.AbsoluteUri);
            Assert.Equal(SpecifierType.Bare, resolved.Type);
        }

        public class TestModuleLoader : DefaultModuleLoader
        {
            public TestModuleLoader(string basePath)
                : base(basePath)
            {
            }

            protected override bool FileExists(Uri uri)
            {
                return true;
            }

            protected override string ReadAllText(Uri uri)
            {
                throw new NotImplementedException();
            }
        }
    }
}