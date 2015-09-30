using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Jint
{
    static class ExceptionExtensions
    {
        public static void Rethrow(this Exception @this)
        {
            UseExceptionDispatchInfo(@this);

            var internalPreserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);

            if (internalPreserveStackTrace != null)
            {
                internalPreserveStackTrace.Invoke(@this, new object[0]);
                throw @this;
            }

            throw @this;
        }

        private static void UseExceptionDispatchInfo(Exception @this)
        {
            var type = Type.GetType("System.Runtime.ExceptionServices.ExceptionDispatchInfo, mscorlib");

            if (type != null)
            {
                var info = type.GetMethod("Capture").Invoke(null, new object[] { @this });
                var throwMethod = type.GetMethod("Throw");
                //var call = Expression.Call(Expression.Constant(info), throwMethod);
                //Expression.Lambda<Action>()
                var action = (Action)Delegate.CreateDelegate(typeof(Action), info, type.GetMethod("Throw"));
                action();
                //info.Throw();
            }
        }
    }
}
