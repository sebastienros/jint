#nullable enable

using Jint.NodeCompat;
using Jint.Runtime;

namespace Jint.Tests.Runtime.NodeCompat;

/// <summary>
/// The opt-in <c>node:path</c> builtin module - https://nodejs.org/api/path.html - against Node's own
/// documented behaviour, and against the corners the documentation does not describe.
/// </summary>
/// <remarks>
/// <para>
/// The expectations in <see cref="MatchesNode"/> were produced by running each expression under a real Node
/// (v24) with <c>process.cwd()</c> stubbed to the working directory this fixture configures, on the
/// <c>win32</c> platform - so <c>path.posix</c>'s own view of that directory is <c>/base</c>, exactly as
/// Node's <c>posixCwd</c> derives it. Every case names its flavour explicitly, so none of them depends on
/// which platform the test run happens on.
/// </para>
/// <para>
/// The list is deliberately long. <c>path</c> is the module whose edge cases bite - a trailing separator that
/// <c>normalize</c> keeps and <c>resolve</c> drops, a <c>..</c> that stops popping at a root, a drive-relative
/// <c>c:foo</c>, a UNC share that is its own directory - and none of them is expressible as a rule that could
/// be checked once.
/// </para>
/// </remarks>
public class PathTests
{
    private const string WorkingDirectory = @"C:\base";

    /// <summary>
    /// An engine with <c>node:path</c> imported and its default export bound to the global <c>path</c>, which
    /// is the shape <c>const path = require('node:path')</c> gives a script.
    /// </summary>
    private static Engine PathEngine(string platform = "win32", string workingDirectory = WorkingDirectory)
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o =>
        {
            o.Platform = platform;
            o.WorkingDirectory = workingDirectory;
        }));

        engine.SetValue("path", engine.Modules.Import("node:path").Get("default"));
        return engine;
    }

    [TestCase("path.posix.join('/foo', 'bar', 'baz/asdf', 'quux', '..')", "/foo/bar/baz/asdf")]
    [TestCase("path.posix.join('.', 'x/b', '..', '/b/c.js')", "x/b/c.js")]
    [TestCase("path.posix.join('/.', 'x/b', '..', '/b/c.js')", "/x/b/c.js")]
    [TestCase("path.posix.join('/foo', '../../../bar')", "/bar")]
    [TestCase("path.posix.join('foo', '../../../bar')", "../../bar")]
    [TestCase("path.posix.join('foo/', '../../../bar')", "../../bar")]
    [TestCase("path.posix.join('a', 'b', 'c')", "a/b/c")]
    [TestCase("path.posix.join('a/', 'b/', 'c/')", "a/b/c/")]
    [TestCase("path.posix.join('')", ".")]
    [TestCase("path.posix.join('', '')", ".")]
    [TestCase("path.posix.join('/', '')", "/")]
    [TestCase("path.posix.join('/', 'foo')", "/foo")]
    [TestCase("path.posix.join('//foo', 'bar')", "/foo/bar")]
    [TestCase("path.posix.join('foo', '')", "foo")]
    [TestCase("path.posix.join('', 'foo')", "foo")]
    [TestCase("path.posix.join('.', '.', '.')", ".")]
    [TestCase("path.posix.join('..', '..')", "../..")]
    [TestCase("path.posix.join()", ".")]
    [TestCase("path.posix.normalize('/foo/bar//baz/asdf/quux/..')", "/foo/bar/baz/asdf")]
    [TestCase("path.posix.normalize('')", ".")]
    [TestCase("path.posix.normalize('.')", ".")]
    [TestCase("path.posix.normalize('./')", "./")]
    [TestCase("path.posix.normalize('/')", "/")]
    [TestCase("path.posix.normalize('//')", "/")]
    [TestCase("path.posix.normalize('///')", "/")]
    [TestCase("path.posix.normalize('a//b//../b')", "a/b")]
    [TestCase("path.posix.normalize('a//b//./c')", "a/b/c")]
    [TestCase("path.posix.normalize('a//b//.')", "a/b")]
    [TestCase("path.posix.normalize('../../')", "../../")]
    [TestCase("path.posix.normalize('../../foo')", "../../foo")]
    [TestCase("path.posix.normalize('/../../foo')", "/foo")]
    [TestCase("path.posix.normalize('foo/bar/')", "foo/bar/")]
    [TestCase("path.posix.normalize('foo/bar/..')", "foo")]
    [TestCase("path.posix.normalize('foo/bar/../..')", ".")]
    [TestCase("path.posix.normalize('foo/bar/../../..')", "..")]
    [TestCase("path.posix.normalize('foo/bar/../../../..')", "../..")]
    [TestCase("path.posix.normalize('/foo/../..')", "/")]
    [TestCase("path.posix.normalize('a/b/c/../../../x/y/z')", "x/y/z")]
    [TestCase("path.posix.normalize('.././.././..')", "../../..")]
    [TestCase("path.posix.normalize('bar/foo../../')", "bar/")]
    [TestCase("path.posix.normalize('bar/foo../..')", "bar")]
    [TestCase("path.posix.normalize('..a/b/c')", "..a/b/c")]
    [TestCase("path.posix.normalize('a\\\\b')", "a\\b")]
    [TestCase("path.posix.resolve('/foo/bar', './baz')", "/foo/bar/baz")]
    [TestCase("path.posix.resolve('/foo/bar', '/tmp/file/')", "/tmp/file")]
    [TestCase("path.posix.resolve('wwwroot', 'static_files/png/', '../gif/image.gif')", "/base/wwwroot/static_files/gif/image.gif")]
    [TestCase("path.posix.resolve()", "/base")]
    [TestCase("path.posix.resolve('')", "/base")]
    [TestCase("path.posix.resolve('.')", "/base")]
    [TestCase("path.posix.resolve('a')", "/base/a")]
    [TestCase("path.posix.resolve('a', '')", "/base/a")]
    [TestCase("path.posix.resolve('/a/b', '..')", "/a")]
    [TestCase("path.posix.resolve('/a/b/', '../../../..')", "/")]
    [TestCase("path.posix.resolve('/', '/')", "/")]
    [TestCase("path.posix.resolve('/foo/tmp.3/', '../tmp.3/cycles/root.js')", "/foo/tmp.3/cycles/root.js")]
    [TestCase("path.posix.relative('/data/orandea/test/aaa', '/data/orandea/impl/bbb')", "../../impl/bbb")]
    [TestCase("path.posix.relative('/var/lib', '/var')", "..")]
    [TestCase("path.posix.relative('/var/lib', '/bin')", "../../bin")]
    [TestCase("path.posix.relative('/var/lib', '/var/lib')", "")]
    [TestCase("path.posix.relative('/var/lib', '/var/apache')", "../apache")]
    [TestCase("path.posix.relative('/var/', '/var/lib')", "lib")]
    [TestCase("path.posix.relative('/', '/var/lib')", "var/lib")]
    [TestCase("path.posix.relative('/foo/test', '/foo/test/bar/package.json')", "bar/package.json")]
    [TestCase("path.posix.relative('/Users/a/web/b/test/mails', '/Users/a/web/b')", "../..")]
    [TestCase("path.posix.relative('foo/bar/baz-quux', 'foo/bar/baz')", "../baz")]
    [TestCase("path.posix.relative('foo/bar/baz', 'foo/bar/baz-quux')", "../baz-quux")]
    [TestCase("path.posix.relative('', '')", "")]
    [TestCase("path.posix.relative('', 'foo')", "foo")]
    [TestCase("path.posix.dirname('/foo/bar/baz/asdf/quux')", "/foo/bar/baz/asdf")]
    [TestCase("path.posix.dirname('/a/b/')", "/a")]
    [TestCase("path.posix.dirname('/a/b')", "/a")]
    [TestCase("path.posix.dirname('/a')", "/")]
    [TestCase("path.posix.dirname('a')", ".")]
    [TestCase("path.posix.dirname('a/')", ".")]
    [TestCase("path.posix.dirname('/')", "/")]
    [TestCase("path.posix.dirname('//')", "/")]
    [TestCase("path.posix.dirname('///')", "/")]
    [TestCase("path.posix.dirname('//a')", "//")]
    [TestCase("path.posix.dirname('')", ".")]
    [TestCase("path.posix.dirname('foo')", ".")]
    [TestCase("path.posix.basename('/foo/bar/baz/asdf/quux.html')", "quux.html")]
    [TestCase("path.posix.basename('/foo/bar/baz/asdf/quux.html', '.html')", "quux")]
    [TestCase("path.posix.basename('/a/b/')", "b")]
    [TestCase("path.posix.basename('/a/b//')", "b")]
    [TestCase("path.posix.basename('basename.ext')", "basename.ext")]
    [TestCase("path.posix.basename('basename.ext/')", "basename.ext")]
    [TestCase("path.posix.basename('basename.ext//')", "basename.ext")]
    [TestCase("path.posix.basename('aaa/bbb', '/bbb')", "bbb")]
    [TestCase("path.posix.basename('aaa/bbb', 'a/bbb')", "bbb")]
    [TestCase("path.posix.basename('aaa/bbb', 'bbb')", "bbb")]
    [TestCase("path.posix.basename('aaa/bbb//', 'bbb')", "bbb")]
    [TestCase("path.posix.basename('aaa/bbb', 'bb')", "b")]
    [TestCase("path.posix.basename('aaa/bbb', 'b')", "bb")]
    [TestCase("path.posix.basename('/aaa/bbb', '/bbb')", "bbb")]
    [TestCase("path.posix.basename('a', 'a')", "")]
    [TestCase("path.posix.basename('')", "")]
    [TestCase("path.posix.basename('/')", "")]
    [TestCase("path.posix.basename('///')", "")]
    [TestCase("path.posix.basename('///', 'x')", "///")]
    [TestCase("path.posix.basename('/dir/basename.ext')", "basename.ext")]
    [TestCase("path.posix.basename('.')", ".")]
    [TestCase("path.posix.basename('..')", "..")]
    [TestCase("path.posix.extname('index.html')", ".html")]
    [TestCase("path.posix.extname('index.coffee.md')", ".md")]
    [TestCase("path.posix.extname('index.')", ".")]
    [TestCase("path.posix.extname('index')", "")]
    [TestCase("path.posix.extname('.index')", "")]
    [TestCase("path.posix.extname('.index.md')", ".md")]
    [TestCase("path.posix.extname('')", "")]
    [TestCase("path.posix.extname('/path/to/file')", "")]
    [TestCase("path.posix.extname('/path/to/file.ext')", ".ext")]
    [TestCase("path.posix.extname('/path.to/file.ext')", ".ext")]
    [TestCase("path.posix.extname('/path.to/.file')", "")]
    [TestCase("path.posix.extname('file.')", ".")]
    [TestCase("path.posix.extname('.')", "")]
    [TestCase("path.posix.extname('..')", "")]
    [TestCase("path.posix.extname('file..')", ".")]
    [TestCase("path.posix.extname('..file')", ".file")]
    [TestCase("path.posix.extname('..file.baz')", ".baz")]
    [TestCase("path.posix.extname('/dir/file.')", ".")]
    [TestCase("String(path.posix.isAbsolute('/foo/bar'))", "true")]
    [TestCase("String(path.posix.isAbsolute('/baz/..'))", "true")]
    [TestCase("String(path.posix.isAbsolute('qux/'))", "false")]
    [TestCase("String(path.posix.isAbsolute('.'))", "false")]
    [TestCase("String(path.posix.isAbsolute(''))", "false")]
    [TestCase("JSON.stringify(path.posix.parse('/home/user/dir/file.txt'))", "{\"root\":\"/\",\"dir\":\"/home/user/dir\",\"base\":\"file.txt\",\"ext\":\".txt\",\"name\":\"file\"}")]
    [TestCase("JSON.stringify(path.posix.parse('/'))", "{\"root\":\"/\",\"dir\":\"/\",\"base\":\"\",\"ext\":\"\",\"name\":\"\"}")]
    [TestCase("JSON.stringify(path.posix.parse(''))", "{\"root\":\"\",\"dir\":\"\",\"base\":\"\",\"ext\":\"\",\"name\":\"\"}")]
    [TestCase("JSON.stringify(path.posix.parse('.'))", "{\"root\":\"\",\"dir\":\"\",\"base\":\".\",\"ext\":\"\",\"name\":\".\"}")]
    [TestCase("JSON.stringify(path.posix.parse('..'))", "{\"root\":\"\",\"dir\":\"\",\"base\":\"..\",\"ext\":\"\",\"name\":\"..\"}")]
    [TestCase("JSON.stringify(path.posix.parse('file.txt'))", "{\"root\":\"\",\"dir\":\"\",\"base\":\"file.txt\",\"ext\":\".txt\",\"name\":\"file\"}")]
    [TestCase("JSON.stringify(path.posix.parse('/file'))", "{\"root\":\"/\",\"dir\":\"/\",\"base\":\"file\",\"ext\":\"\",\"name\":\"file\"}")]
    [TestCase("JSON.stringify(path.posix.parse('/.foo'))", "{\"root\":\"/\",\"dir\":\"/\",\"base\":\".foo\",\"ext\":\"\",\"name\":\".foo\"}")]
    [TestCase("JSON.stringify(path.posix.parse('/a/b/'))", "{\"root\":\"/\",\"dir\":\"/a\",\"base\":\"b\",\"ext\":\"\",\"name\":\"b\"}")]
    [TestCase("path.posix.format({ root: '/ignored', dir: '/home/user/dir', base: 'file.txt' })", "/home/user/dir/file.txt")]
    [TestCase("path.posix.format({ root: '/', base: 'file.txt', ext: 'ignored' })", "/file.txt")]
    [TestCase("path.posix.format({ root: '/', name: 'file', ext: '.txt' })", "/file.txt")]
    [TestCase("path.posix.format({ root: '/', name: 'file', ext: 'txt' })", "/file.txt")]
    [TestCase("path.posix.format({})", "")]
    [TestCase("path.posix.format({ dir: 'a', base: 'b' })", "a/b")]
    [TestCase("path.posix.format({ name: 'file' })", "file")]
    [TestCase("path.win32.join('/foo', 'bar', 'baz/asdf', 'quux', '..')", "\\foo\\bar\\baz\\asdf")]
    [TestCase("path.win32.join('c:/ignore', 'd:\\\\a/b', '..', '/e.exe')", "c:\\ignore\\d:\\a\\e.exe")]
    [TestCase("path.win32.join('c:/ignore', 'c:/some/file')", "c:\\ignore\\c:\\some\\file")]
    [TestCase("path.win32.join('//server/share', '..', 'relative\\\\')", "\\\\server\\share\\relative\\")]
    [TestCase("path.win32.join('c:/', '//')", "c:\\")]
    [TestCase("path.win32.join('c:/', '//dir')", "c:\\dir")]
    [TestCase("path.win32.join('c:/', '//server/share')", "c:\\server\\share")]
    [TestCase("path.win32.join('c:/', '//server//share')", "c:\\server\\share")]
    [TestCase("path.win32.join('c:/', '///some//dir')", "c:\\some\\dir")]
    [TestCase("path.win32.join('C:\\\\foo\\\\tmp.3\\\\', '..\\\\tmp.3\\\\cycles\\\\root.js')", "C:\\foo\\tmp.3\\cycles\\root.js")]
    [TestCase("path.win32.join('//server', 'share')", "\\\\server\\share\\")]
    [TestCase("path.win32.join('\\\\\\\\server', 'share')", "\\\\server\\share\\")]
    [TestCase("path.win32.join('.', 'x/b', '..', '/b/c.js')", "x\\b\\c.js")]
    [TestCase("path.win32.join('foo', '')", "foo")]
    [TestCase("path.win32.join('', 'foo')", "foo")]
    [TestCase("path.win32.join('')", ".")]
    [TestCase("path.win32.join()", ".")]
    [TestCase("path.win32.join('a', 'b', 'c')", "a\\b\\c")]
    [TestCase("path.win32.join('\\\\\\\\foo', 'bar')", "\\\\foo\\bar\\")]
    [TestCase("path.win32.join('\\\\\\\\', 'foo')", "\\foo")]
    [TestCase("path.win32.normalize('C:\\\\temp\\\\\\\\foo\\\\bar\\\\..\\\\')", "C:\\temp\\foo\\")]
    [TestCase("path.win32.normalize('C:////temp\\\\\\\\/\\\\/\\\\/foo/bar')", "C:\\temp\\foo\\bar")]
    [TestCase("path.win32.normalize('')", ".")]
    [TestCase("path.win32.normalize('.')", ".")]
    [TestCase("path.win32.normalize('/')", "\\")]
    [TestCase("path.win32.normalize('\\\\')", "\\")]
    [TestCase("path.win32.normalize('c:')", "c:.")]
    [TestCase("path.win32.normalize('c:\\\\')", "c:\\")]
    [TestCase("path.win32.normalize('c:foo')", "c:foo")]
    [TestCase("path.win32.normalize('c:foo\\\\bar')", "c:foo\\bar")]
    [TestCase("path.win32.normalize('c:..\\\\foo')", "c:..\\foo")]
    [TestCase("path.win32.normalize('\\\\\\\\server\\\\share\\\\dir\\\\file.ext')", "\\\\server\\share\\dir\\file.ext")]
    [TestCase("path.win32.normalize('\\\\\\\\server\\\\share')", "\\\\server\\share\\")]
    [TestCase("path.win32.normalize('\\\\\\\\server\\\\share\\\\')", "\\\\server\\share\\")]
    [TestCase("path.win32.normalize('\\\\\\\\server\\\\share\\\\..\\\\..\\\\x')", "\\\\server\\share\\x")]
    [TestCase("path.win32.normalize('//server/share/dir/file.ext')", "\\\\server\\share\\dir\\file.ext")]
    [TestCase("path.win32.normalize('a/b/c/../../../x/y/z')", "x\\y\\z")]
    [TestCase("path.win32.normalize('bar\\\\foo..\\\\..')", "bar")]
    [TestCase("path.win32.normalize('bar\\\\foo..\\\\..\\\\')", "bar\\")]
    [TestCase("path.win32.normalize('..\\\\..\\\\')", "..\\..\\")]
    [TestCase("path.win32.normalize('\\\\\\\\.\\\\PHYSICALDRIVE0')", "\\\\.\\PHYSICALDRIVE0")]
    [TestCase("path.win32.normalize('\\\\\\\\?\\\\UNC\\\\server\\\\share\\\\dir')", "\\\\?\\UNC\\server\\share\\dir")]
    [TestCase("path.win32.resolve('c:/blah\\\\blah', 'd:/games', 'c:../a')", "c:\\blah\\a")]
    [TestCase("path.win32.resolve('c:/ignore', 'd:\\\\a/b', '..', '/e.exe')", "d:\\e.exe")]
    [TestCase("path.win32.resolve('c:/blah\\\\blah', 'd:/games', 'c:/a')", "c:\\a")]
    [TestCase("path.win32.resolve()", "C:\\base")]
    [TestCase("path.win32.resolve('')", "C:\\base")]
    [TestCase("path.win32.resolve('.')", "C:\\base")]
    [TestCase("path.win32.resolve('a')", "C:\\base\\a")]
    [TestCase("path.win32.resolve('\\\\\\\\server\\\\share', 'file')", "\\\\server\\share\\file")]
    [TestCase("path.win32.resolve('c:/', 'foo')", "c:\\foo")]
    [TestCase("path.win32.resolve('c:foo')", "c:\\base\\foo")]
    [TestCase("path.win32.resolve('C:\\\\foo\\\\bar', '..')", "C:\\foo")]
    [TestCase("path.win32.relative('C:\\\\orandea\\\\test\\\\aaa', 'C:\\\\orandea\\\\impl\\\\bbb')", "..\\..\\impl\\bbb")]
    [TestCase("path.win32.relative('c:/blah\\\\blah', 'd:/games')", "d:\\games")]
    [TestCase("path.win32.relative('c:/aaaa/bbbb', 'c:/aaaa')", "..")]
    [TestCase("path.win32.relative('c:/aaaa/bbbb', 'c:/cccc')", "..\\..\\cccc")]
    [TestCase("path.win32.relative('c:/aaaa/bbbb', 'c:/aaaa/bbbb')", "")]
    [TestCase("path.win32.relative('c:/aaaa/bbbb', 'c:/aaaa/cccc')", "..\\cccc")]
    [TestCase("path.win32.relative('c:/aaaa/', 'c:/aaaa/cccc')", "cccc")]
    [TestCase("path.win32.relative('c:/', 'c:\\\\aaaa\\\\bbbb')", "aaaa\\bbbb")]
    [TestCase("path.win32.relative('C:\\\\foo\\\\bar\\\\baz-quux', 'C:\\\\foo\\\\bar\\\\baz')", "..\\baz")]
    [TestCase("path.win32.relative('\\\\\\\\foo\\\\bar', '\\\\\\\\foo\\\\bar\\\\baz')", "baz")]
    [TestCase("path.win32.relative('\\\\\\\\foo\\\\bar\\\\baz', '\\\\\\\\foo\\\\bar')", "..")]
    [TestCase("path.win32.relative('C:\\\\baz-quux', 'C:\\\\baz')", "..\\baz")]
    [TestCase("path.win32.dirname('c:\\\\')", "c:\\")]
    [TestCase("path.win32.dirname('c:\\\\foo')", "c:\\")]
    [TestCase("path.win32.dirname('c:\\\\foo\\\\')", "c:\\")]
    [TestCase("path.win32.dirname('c:\\\\foo\\\\bar')", "c:\\foo")]
    [TestCase("path.win32.dirname('c:foo')", "c:")]
    [TestCase("path.win32.dirname('\\\\\\\\unc\\\\share')", "\\\\unc\\share")]
    [TestCase("path.win32.dirname('\\\\\\\\unc\\\\share\\\\foo')", "\\\\unc\\share\\")]
    [TestCase("path.win32.dirname('\\\\\\\\unc\\\\share\\\\foo\\\\bar')", "\\\\unc\\share\\foo")]
    [TestCase("path.win32.dirname('/a/b/')", "/a")]
    [TestCase("path.win32.dirname('/a/b')", "/a")]
    [TestCase("path.win32.dirname('/a')", "/")]
    [TestCase("path.win32.dirname('')", ".")]
    [TestCase("path.win32.dirname('/')", "/")]
    [TestCase("path.win32.dirname('////')", "/")]
    [TestCase("path.win32.dirname('foo')", ".")]
    [TestCase("path.win32.basename('C:\\\\temp\\\\myfile.html')", "myfile.html")]
    [TestCase("path.win32.basename('C:\\\\foo.html', '.html')", "foo")]
    [TestCase("path.win32.basename('C:\\\\foo.HTML', '.html')", "foo.HTML")]
    [TestCase("path.win32.basename('C:')", "")]
    [TestCase("path.win32.basename('C:.')", ".")]
    [TestCase("path.win32.basename('C:\\\\')", "")]
    [TestCase("path.win32.basename('C:\\\\dir\\\\base.ext')", "base.ext")]
    [TestCase("path.win32.basename('C:\\\\basename.ext')", "basename.ext")]
    [TestCase("path.win32.basename('C:basename.ext')", "basename.ext")]
    [TestCase("path.win32.basename('C:basename.ext\\\\')", "basename.ext")]
    [TestCase("path.win32.basename('\\\\dir\\\\basename.ext')", "basename.ext")]
    [TestCase("path.win32.basename('foo')", "foo")]
    [TestCase("path.win32.basename('a', 'a')", "")]
    [TestCase("path.win32.extname('C:\\\\path\\\\dir\\\\file.txt')", ".txt")]
    [TestCase("path.win32.extname('C:\\\\path.to\\\\file')", "")]
    [TestCase("path.win32.extname('C:file.ext')", ".ext")]
    [TestCase("path.win32.extname('C:.ext')", "")]
    [TestCase("path.win32.extname('.')", "")]
    [TestCase("path.win32.extname('..')", "")]
    [TestCase("String(path.win32.isAbsolute('//server'))", "true")]
    [TestCase("String(path.win32.isAbsolute('\\\\\\\\server'))", "true")]
    [TestCase("String(path.win32.isAbsolute('C:/foo/..'))", "true")]
    [TestCase("String(path.win32.isAbsolute('C:\\\\foo\\\\..'))", "true")]
    [TestCase("String(path.win32.isAbsolute('bar\\\\baz'))", "false")]
    [TestCase("String(path.win32.isAbsolute('bar/baz'))", "false")]
    [TestCase("String(path.win32.isAbsolute('.'))", "false")]
    [TestCase("String(path.win32.isAbsolute('C:'))", "false")]
    [TestCase("String(path.win32.isAbsolute('C:cwd/another'))", "false")]
    [TestCase("JSON.stringify(path.win32.parse('C:\\\\path\\\\dir\\\\file.txt'))", "{\"root\":\"C:\\\\\",\"dir\":\"C:\\\\path\\\\dir\",\"base\":\"file.txt\",\"ext\":\".txt\",\"name\":\"file\"}")]
    [TestCase("JSON.stringify(path.win32.parse('C:\\\\'))", "{\"root\":\"C:\\\\\",\"dir\":\"C:\\\\\",\"base\":\"\",\"ext\":\"\",\"name\":\"\"}")]
    [TestCase("JSON.stringify(path.win32.parse('C:'))", "{\"root\":\"C:\",\"dir\":\"C:\",\"base\":\"\",\"ext\":\"\",\"name\":\"\"}")]
    [TestCase("JSON.stringify(path.win32.parse('\\\\\\\\server\\\\share'))", "{\"root\":\"\\\\\\\\server\\\\share\",\"dir\":\"\\\\\\\\server\\\\share\",\"base\":\"\",\"ext\":\"\",\"name\":\"\"}")]
    [TestCase("JSON.stringify(path.win32.parse('\\\\\\\\server\\\\share\\\\file.txt'))", "{\"root\":\"\\\\\\\\server\\\\share\\\\\",\"dir\":\"\\\\\\\\server\\\\share\\\\\",\"base\":\"file.txt\",\"ext\":\".txt\",\"name\":\"file\"}")]
    [TestCase("JSON.stringify(path.win32.parse('c:\\\\foo\\\\.bar.baz'))", "{\"root\":\"c:\\\\\",\"dir\":\"c:\\\\foo\",\"base\":\".bar.baz\",\"ext\":\".baz\",\"name\":\".bar\"}")]
    [TestCase("JSON.stringify(path.win32.parse(''))", "{\"root\":\"\",\"dir\":\"\",\"base\":\"\",\"ext\":\"\",\"name\":\"\"}")]
    [TestCase("JSON.stringify(path.win32.parse('\\\\'))", "{\"root\":\"\\\\\",\"dir\":\"\\\\\",\"base\":\"\",\"ext\":\"\",\"name\":\"\"}")]
    [TestCase("path.win32.format({ dir: 'C:\\\\path\\\\dir', base: 'file.txt' })", "C:\\path\\dir\\file.txt")]
    [TestCase("path.win32.format({ root: 'C:\\\\', base: 'file.txt' })", "C:\\file.txt")]
    [TestCase("path.win32.format({ root: 'C:\\\\', name: 'file', ext: 'txt' })", "C:\\file.txt")]
    [TestCase("path.win32.toNamespacedPath('C:\\\\foo')", "\\\\?\\C:\\foo")]
    [TestCase("path.win32.toNamespacedPath('\\\\\\\\server\\\\share\\\\foo')", "\\\\?\\UNC\\server\\share\\foo")]
    [TestCase("path.win32.toNamespacedPath('\\\\\\\\?\\\\C:\\\\foo')", "\\\\?\\C:\\foo")]
    [TestCase("path.win32.toNamespacedPath('')", "")]
    [TestCase("path.posix.toNamespacedPath('/foo/bar')", "/foo/bar")]
    [TestCase("path.posix.sep", "/")]
    [TestCase("path.win32.sep", "\\")]
    [TestCase("path.posix.delimiter", ":")]
    [TestCase("path.win32.delimiter", ";")]

    public void MatchesNode(string expression, string expected)
    {
        var engine = PathEngine();

        engine.Evaluate(expression).AsString().Should().Be(expected, expression);
    }

    /// <summary>
    /// "The default operation of the <c>node:path</c> module varies based on the operating system on which a
    /// Node.js application is running" — here, on the platform the <c>process</c> shim would report.
    /// </summary>
    [TestCase("win32", "\\", ";")]
    [TestCase("linux", "/", ":")]
    [TestCase("darwin", "/", ":")]
    public void TheDefaultFlavourFollowsTheConfiguredPlatform(string platform, string separator, string delimiter)
    {
        var engine = PathEngine(platform);

        engine.Evaluate("path.sep").AsString().Should().Be(separator);
        engine.Evaluate("path.delimiter").AsString().Should().Be(delimiter);
        engine.Evaluate("path.join('a', 'b')").AsString().Should().Be("a" + separator + "b");
    }

    /// <summary>
    /// A default engine's platform is the one it is running on, which is the same answer
    /// <c>process.platform</c> gives — the two shims read one detection.
    /// </summary>
    [Test]
    public void TheDefaultPlatformAgreesWithTheProcessShim()
    {
        var engine = new Engine(options => options.UseNodeProcess().UseNodeBuiltinModules());
        engine.SetValue("path", engine.Modules.Import("node:path").Get("default"));

        var expected = engine.Evaluate("process.platform").AsString() == "win32" ? "\\" : "/";

        engine.Evaluate("path.sep").AsString().Should().Be(expected);
    }

    /// <summary>
    /// Both flavours are reachable from either, and each is its own <c>posix</c>/<c>win32</c> — Node's
    /// <c>posix.win32 === win32</c> and <c>win32.posix === posix</c>.
    /// </summary>
    [Test]
    public void TheTwoFlavoursCrossReferenceEachOther()
    {
        var engine = PathEngine();

        engine.Evaluate("path.posix.win32 === path.win32").AsBoolean().Should().BeTrue();
        engine.Evaluate("path.win32.posix === path.posix").AsBoolean().Should().BeTrue();
        engine.Evaluate("path.posix.posix === path.posix").AsBoolean().Should().BeTrue();
        engine.Evaluate("path.win32.win32 === path.win32").AsBoolean().Should().BeTrue();
        engine.Evaluate("path.win32 === path").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>node:path/posix</c> and <c>node:path/win32</c> are the two flavours as modules of their own, which
    /// is how Node exposes them since v15.3.0.
    /// </summary>
    [TestCase("node:path/posix", "/")]
    [TestCase("node:path/win32", "\\")]
    public void TheFlavoursAreImportableAsModulesOfTheirOwn(string specifier, string separator)
    {
        var engine = PathEngine();

        engine.SetValue("flavour", engine.Modules.Import(specifier).Get("default"));

        engine.Evaluate("flavour.sep").AsString().Should().Be(separator);
    }

    /// <summary>
    /// Named exports beside the default one, so <c>import { join, sep } from 'node:path'</c> works.
    /// </summary>
    [Test]
    public void ExposesNamedExports()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o => o.Platform = "linux"));
        engine.Modules.Add("main", "import { join, resolve, sep, posix, win32 } from 'node:path'; export const result = [join('a', 'b'), typeof resolve, sep, posix.sep, win32.sep].join(',');");

        engine.Modules.Import("main").Get("result").AsString().Should().Be("a/b,function,/,/,\\");
    }

    /// <summary>
    /// <c>path.resolve()</c> and <c>path.relative()</c> answer from the configured working directory and never
    /// from the real one — <see cref="NodeBuiltinModuleOptions.WorkingDirectory"/>'s whole point.
    /// </summary>
    [Test]
    public void ResolveUsesTheConfiguredWorkingDirectory()
    {
        var engine = PathEngine("linux", "/srv/app");

        engine.Evaluate("path.resolve()").AsString().Should().Be("/srv/app");
        engine.Evaluate("path.resolve('x')").AsString().Should().Be("/srv/app/x");
        engine.Evaluate("path.relative('', '/srv/app/x')").AsString().Should().Be("x");
        engine.Evaluate("path.resolve()").AsString().Should().NotBe(Environment.CurrentDirectory);
    }

    /// <summary>
    /// Node's <c>posixCwd</c>: on Windows the working directory has its separators turned around and its drive
    /// dropped before <c>path.posix</c> sees it, so the two flavours never disagree about where "here" is.
    /// </summary>
    [Test]
    public void ThePosixFlavourSeesTheWorkingDirectoryWithoutItsDrive()
    {
        var engine = PathEngine("win32", @"C:\srv\app");

        engine.Evaluate("path.win32.resolve()").AsString().Should().Be(@"C:\srv\app");
        engine.Evaluate("path.posix.resolve()").AsString().Should().Be("/srv/app");
    }

    /// <summary>
    /// "Throws a <c>TypeError</c> if any of the path segments is not a string" — the argument has to
    /// <em>be</em> a string, so a number is refused rather than coerced.
    /// </summary>
    [TestCase("path.join('a', 1)")]
    [TestCase("path.join(1, 'a')")]
    [TestCase("path.resolve(1)")]
    [TestCase("path.normalize(null)")]
    [TestCase("path.dirname(undefined)")]
    [TestCase("path.basename({})")]
    [TestCase("path.basename('a', 1)")]
    [TestCase("path.extname(true)")]
    [TestCase("path.isAbsolute(1)")]
    [TestCase("path.relative('a', 1)")]
    [TestCase("path.relative(1, 'a')")]
    [TestCase("path.parse(1)")]
    [TestCase("path.format(null)")]
    [TestCase("path.format('x')")]
    public void RefusesAnArgumentOfTheWrongType(string expression)
    {
        var engine = PathEngine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate(expression))!;

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        exception.Message.Should().Contain("must be of type");
    }

    /// <summary>
    /// <c>path.toNamespacedPath</c> is the exception: "if <c>path</c> is not a string, <c>path</c> will be
    /// returned without modifications".
    /// </summary>
    [Test]
    public void ToNamespacedPathReturnsANonStringUnchanged()
    {
        var engine = PathEngine();

        engine.Evaluate("path.toNamespacedPath(42)").AsNumber().Should().Be(42);
        engine.Evaluate("path.toNamespacedPath(undefined) === undefined").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>path.matchesGlob</c> is deliberately absent, so a script feature-detecting it takes its other branch
    /// rather than receiving an approximation of glob semantics.
    /// </summary>
    [Test]
    public void MatchesGlobIsAbsent()
    {
        var engine = PathEngine();

        engine.Evaluate("typeof path.matchesGlob").AsString().Should().Be("undefined");
        engine.Evaluate("typeof path.posix.matchesGlob").AsString().Should().Be("undefined");
    }
}
