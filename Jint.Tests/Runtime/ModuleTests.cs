using Xunit;

namespace Jint.Tests.Runtime
{
    public class ModuleTests
    {
        [Fact]
        public void CanLoadModuleImports()
        {
            var engine = new Engine(options => options.EnableModules(basePath: "Scripts"));
            var obj = engine.Evaluate(@"
import { User } from './Runtime/Scripts/module-a';

const user = new User('John');

return {
    userName: user.name
}
").AsObject();

            Assert.Equal("John", obj["userName"].AsString());
        }
    }
}