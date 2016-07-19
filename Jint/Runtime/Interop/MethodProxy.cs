using Jint.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Jint.Runtime.Interop
{
    public class MethodProxy
    {
        private MethodBase _methodBase;
        private Func<object, object[], object> _methodProxy;

        public MethodBase UnderlyingMethod
        {
            get { return _methodBase; }
        }

        public MethodProxy(MethodBase methodBase)
        {
            _methodBase = methodBase;
        }

        public JsValue Invoke(Engine engine, object self, object[] parameters)
        {
#if __IOS__
            return JsValue.FromObject(engine, _methodBase.Invoke(self, parameters));
#else
            if (_methodProxy == null)
            {
                var methodInfo = _methodBase as MethodInfo;
                if (methodInfo == null)
                {
                    // If it is not a method, it is probably a constructor. If so, why are we here?
                    throw new InvalidOperationException("Cannot invoke constructor directly");
                }

                var selfArgument = Expression.Parameter(typeof(object));
                var argumentsArgument = Expression.Parameter(typeof(object[]));

                var arguments = methodInfo.GetParameters().Select((parameter, index) =>
                {
                    return Expression.Convert(
                        Expression.ArrayAccess(argumentsArgument, Expression.Constant(index)),
                        parameter.ParameterType);
                });

                Expression body;

                if (methodInfo.IsStatic)
                {
                    body = Expression.Call(
                        methodInfo,
                        arguments);
                }
                else
                {
                    body = Expression.Call(
                        Expression.Convert(selfArgument, methodInfo.DeclaringType),
                        methodInfo,
                        arguments);
                }

                if (methodInfo.ReturnType == typeof(void))
                {
                    body = Expression.Block(body, Expression.Constant(null));
                }

                body = Expression.Convert(body, typeof(object));

                _methodProxy = Expression.Lambda<Func<object, object[], object>>(body, selfArgument, argumentsArgument).Compile();
            }

            return JsValue.FromObject(engine, _methodProxy(self, parameters));
#endif
        }
    }
}
