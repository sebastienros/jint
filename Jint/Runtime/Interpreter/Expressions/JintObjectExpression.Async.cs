using Jint.Collections;
using Jint.Runtime.Descriptors;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/#sec-object-initializer
    /// </summary>
    internal sealed partial class JintObjectExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            return _canBuildFast
                ? await BuildObjectFastAsync()
                : BuildObjectNormal();
        }



        /// <summary>
        /// Version that can safely build plain object with only normal init/data fields fast.
        /// </summary>
        private async Task<object> BuildObjectFastAsync()
        {
            var obj = _engine.Object.Construct(0);
            if (_properties.Length == 0)
            {
                return obj;
            }

            var properties = new PropertyDictionary(_properties.Length, checkExistingKeys: true);
            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var valueExpression = _valueExpressions[i];
                var propValue = (await valueExpression.GetValueAsync()).Clone();
                properties[objectProperty!._key] = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
            }
            obj.SetProperties(properties);
            return obj;
        }
    }
}