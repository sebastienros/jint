using Jint.Browser.Dom;
using Jint.Browser.Dom.Files;
using Jint.WebApi.Files;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeJsReceiverBindingTests
{
    [Test]
    public void BlobBindingsShareNativeBytesAndSliceWithoutReplacingTheCorePrototype()
    {
        using var dom = DomTestFixture.Create("");
        dom.Engine.SetValue("blobBindingPrototype", DomRealm.Of(dom.Engine).PrototypeOf(DomInterfaces.Blob));
        dom.Execute("var file=new File(['abcde'],'native.txt'); var order=[]; var sliced=blobBindingPrototype.slice.call(file,{valueOf(){order.push('start');return 1}},{valueOf(){order.push('end');return 3}},{toString(){order.push('type');return 'TEXT/PLAIN'}});");
        dom.Bool("order.join(',')==='start,end,type' && sliced.size===2 && sliced.type==='text/plain' && Object.getPrototypeOf(sliced)===Blob.prototype && Object.getPrototypeOf(file)===File.prototype").Should().BeTrue();
        dom.Bool("Object.getOwnPropertyDescriptor(blobBindingPrototype,'size').get.call(file)===5 && blobBindingPrototype.slice.call(file,-2).size===2").Should().BeTrue();
        dom.Text("(()=>{try{blobBindingPrototype.close.call(file)}catch(error){return error.name}})()").Should().Be("NotSupportedError");
        dom.Bool("!('close' in Blob.prototype) && !('isClosed' in Blob.prototype)").Should().BeTrue();
        dom.Bool("(()=>{let converted=false;try{blobBindingPrototype.slice.call({}, {valueOf(){converted=true;return 0}})}catch(error){return error instanceof TypeError && !converted}})()").Should().BeTrue();
    }

    [Test]
    public void NavigatorBindingsDeriveAppVersionFromTheCurrentConfiguredUserAgent()
    {
        using var dom = DomTestFixture.Create("");
        dom.Engine.SetValue("navigatorBindingPrototype", DomRealm.Of(dom.Engine).PrototypeOf(DomInterfaces.Navigator));
        dom.Execute("var versionGetter=Object.getOwnPropertyDescriptor(navigatorBindingPrototype,'appVersion').get; var userAgentGetter=Object.getOwnPropertyDescriptor(navigatorBindingPrototype,'userAgent').get;");
        dom.Engine.WebApi.UserAgent = "Mozilla/5.0 (native test) Example/1";
        dom.Text("versionGetter.call(navigator)").Should().Be("5.0 (native test) Example/1");
        dom.Text("userAgentGetter.call(navigator)").Should().Be("Mozilla/5.0 (native test) Example/1");
        dom.Engine.WebApi.UserAgent = "mozilla/5.0 (native test)";
        dom.Text("versionGetter.call(navigator)").Should().Be("");
        dom.Bool("Object.getPrototypeOf(navigator)===Navigator.prototype && Navigator.prototype!==navigatorBindingPrototype").Should().BeTrue();
        dom.Bool("(()=>{var order=[];try{navigatorBindingPrototype.registerProtocolHandler.call(navigator,{toString(){order.push(0);return 'web+x'}},{toString(){order.push(1);return '/handler'}},{toString(){order.push(2);return 'name'}})}catch(error){return error.name==='NotSupportedError' && order.join(',')==='0,1,2'}})()").Should().BeTrue();
    }

    [Test]
    public void RetainedFileAndExceptionGettersReadExistingObjectsWithoutReplacingTheirPrototypes()
    {
        using var dom = DomTestFixture.Create("");
        var realm = DomRealm.Of(dom.Engine);
        dom.Engine.SetValue("fileBindingPrototype", realm.PrototypeOf(DomInterfaces.File));
        dom.Engine.SetValue("exceptionBindingPrototype", realm.PrototypeOf(DomInterfaces.DOMException));
        dom.Execute("var file=new File(['abc'],'native.txt',{lastModified:123}); var error=new DOMException('message','NotFoundError');");
        dom.Bool("Object.getPrototypeOf(file)===File.prototype && File.prototype!==fileBindingPrototype && Object.getPrototypeOf(error)===DOMException.prototype").Should().BeTrue();
        dom.Bool("Object.getOwnPropertyDescriptor(fileBindingPrototype,'name').get.call(file)==='native.txt' && Object.getOwnPropertyDescriptor(fileBindingPrototype,'lastModified').get.call(file)===123").Should().BeTrue();
        dom.Bool("Object.getOwnPropertyDescriptor(exceptionBindingPrototype,'code').get.call(error)===error.code").Should().BeTrue();
        dom.Bool("[{},document,new Blob()].every(value=>{try{Object.getOwnPropertyDescriptor(fileBindingPrototype,'name').get.call(value);return false}catch(e){return e instanceof TypeError && /Illegal invocation/.test(e.message)}})").Should().BeTrue();
    }

    [Test]
    public void FileListBindingsReturnTheOriginalFileAndDistinguishMissingIndices()
    {
        using var dom = DomTestFixture.Create("");
        var realm = DomRealm.Of(dom.Engine);
        var file = (JsFile) dom.Evaluate("new File(['abc'],'native.txt')");
        var files = new JsFileList(dom.Engine, dom.Engine.Realm.Intrinsics.Object.PrototypeObject);
        files.Add(file);
        dom.Engine.SetValue("file", file);
        dom.Engine.SetValue("files", files);
        dom.Engine.SetValue("fileListBindingPrototype", realm.PrototypeOf(DomInterfaces.FileList));
        dom.Bool("Object.getOwnPropertyDescriptor(fileListBindingPrototype,'length').get.call(files)===1 && fileListBindingPrototype.item.call(files,0)===file && files[0]===file").Should().BeTrue();
        dom.Bool("fileListBindingPrototype.item.call(files,-1)===null && fileListBindingPrototype.item.call(files,1)===null && files[1]===undefined").Should().BeTrue();
        dom.Bool("(()=>{let converted=false;try{fileListBindingPrototype.item.call({}, {valueOf(){converted=true;return 0}})}catch(e){return e instanceof TypeError && !converted}})()").Should().BeTrue();
    }
}
