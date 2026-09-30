#if NET8_0_OR_GREATER
using Jint.Native;

namespace Jint.Tests.PublicInterface;

public sealed class HostIndexedDbTests
{
    [Test]
    public void HostCanEnableIndexedDbAndPumpAStoredValue()
    {
        using var engine = new Engine(options => options.UseWebApis(WebApiFeatures.IndexedDb));
        var result = engine.Evaluate("""
            new Promise((resolve,reject) => {
                const r=indexedDB.open('host');
                r.onerror=()=>reject(r.error);
                r.onupgradeneeded=()=>r.result.createObjectStore('items');
                r.onsuccess=()=>{
                    const db=r.result,t=db.transaction('items','readwrite'),s=t.objectStore('items');
                    s.put({answer:42},'key');
                    t.oncomplete=()=>{
                        const read=db.transaction('items').objectStore('items').get('key');
                        read.onsuccess=()=>{resolve(read.result.answer);db.close();};
                        read.onerror=()=>reject(read.error);
                    };
                    t.onabort=()=>reject(t.error);
                };
            })
            """);
        engine.Tasks.ProcessTasks();
        result.UnwrapIfPromise().AsNumber().Should().Be(42);
    }
}
#endif
