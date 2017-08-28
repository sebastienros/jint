using Xunit;

namespace Jint.Tests.Runtime
{
    public class ArrowFunctions
    {
        [Fact]
        public void BindingOfThis()
        {
            var engine = new Engine();
            engine.Execute(@"
var adder = {
  base: 1,

  add: function(a) {
    var f = v => v + this.base;
    return f(a);
  },

  addThruCall: function(a) {
    var f = v => v + this.base;
    var b = {
      base: 2
    };

    return f.call(b, a);
  }
};
");
            engine.Execute("adder.add(1)");
            var value = engine.GetCompletionValue();
            Assert.Equal(2, value.AsNumber());

            engine.Execute("adder.addThruCall(1)");
            value = engine.GetCompletionValue();
            Assert.Equal(2, value.AsNumber());
        }

        [Fact]
        public void SimpleCall()
        {
            var engine = new Engine();
            engine.Execute(@"
var materials = [
 'Hydrogen',
  'Helium',
  'Lithium',
  'Beryllium'
];
materials.map(function(material) {return  material.length;}); // [8, 6, 7, 9]

materials.map(material => material.length); // [8, 6, 7, 9]
");
            var value = engine.GetCompletionValue();
            Assert.Equal(4u, value.AsArray().GetLength());
            Assert.Equal(8, value.AsArray().Get("0").AsNumber());
            Assert.Equal(6, value.AsArray().Get("1").AsNumber());
            Assert.Equal(7, value.AsArray().Get("2").AsNumber());
            Assert.Equal(9, value.AsArray().Get("3").AsNumber());
        }
    }
}