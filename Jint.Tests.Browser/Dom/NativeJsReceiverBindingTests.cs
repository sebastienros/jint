using Jint.Browser.Dom;
using Jint.Browser.Dom.Files;
using Jint.WebApi.Files;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeJsReceiverBindingTests
{
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
