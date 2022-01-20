using System;
using System.IO;
using Jint.Runtime.Modules;
using Xunit;

namespace Jint.Tests.Runtime.Modules
{
    public class DefaultModuleLoaderTests
    {
        [Theory]
        [InlineData("./other", @"Z:\project\folder\other.js", "/folder/other")]
        [InlineData("./other.js", @"Z:\project\folder\other.js", "/folder/other")]
        [InlineData("../model/other", @"Z:\project\model\other.js", "/model/other")]
        [InlineData("/model/other", @"Z:\project\model\other.js", "/model/other")]
        public void ShouldResolveRelativePathsOnWindows(string specifier, string expectedPath, string expectedKey)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                // Making this test work on both Windows and Linux would make the test unreadable
                return;
            }

            const string basePath = @"Z:\project";
            const string parentPath = @"Z:\project\folder\script.js";
            var resolver = new DefaultModuleLoader(basePath);

            var resolved = resolver.Resolve(parentPath, specifier);

            Assert.Equal(specifier, resolved.Specifier);
            Assert.Equal(expectedKey, resolved.Key);
            Assert.Equal(expectedPath, resolved.Path?.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(SpecifierType.File, resolved.Type);
        }

        [Theory]
        [InlineData("./other", @"/project/folder/other.js", "/folder/other")]
        [InlineData("./other.js", @"/project/folder/other.js", "/folder/other")]
        [InlineData("../model/other", @"/project/model/other.js", "/model/other")]
        [InlineData("/model/other", @"/project/model/other.js", "/model/other")]
        public void ShouldResolveRelativePathsOnLinux(string specifier, string expectedPath, string expectedKey)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Making this test work on both Windows and Linux would make the test unreadable
                return;
            }

            const string basePath = @"/project";
            const string parentPath = @"/project/folder/script.js";
            var resolver = new DefaultModuleLoader(basePath);

            var resolved = resolver.Resolve(parentPath, specifier);

            Assert.Equal(specifier, resolved.Specifier);
            Assert.Equal(expectedKey, resolved.Key);
            Assert.Equal(expectedPath, resolved.Path);
            Assert.Equal(SpecifierType.File, resolved.Type);
        }

        [Fact]
        public void ShouldResolveBareSpecifiers()
        {
            var resolver = new DefaultModuleLoader("/tmp");

            var resolved = resolver.Resolve(null, "my-module");

            Assert.Equal("my-module", resolved.Specifier);
            Assert.Equal("my-module", resolved.Key);
            Assert.Equal(null, resolved.Path);
            Assert.Equal(SpecifierType.Bare, resolved.Type);
        }
    }
}