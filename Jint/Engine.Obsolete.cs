using System;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Date;
using Jint.Native.Error;
using Jint.Native.Function;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Native.String;

namespace Jint
{
    public partial class Engine
    {
        /// <summary>
        /// Gets the last evaluated statement completion value
        /// </summary>
        [Obsolete("Prefer calling Evaluate which returns last completion value. Execute is for initialization and Evaluate returns final result.")]
        public JsValue GetCompletionValue()
        {
            return _completionValue;
        }

        [Obsolete("Use active realms Intrinsics to access well-known object")]
        public ArrayConstructor Array => Realm.Intrinsics.Array;

        [Obsolete("Use active realms Intrinsics to access well-known object")]
        public DateConstructor Date => Realm.Intrinsics.Date;

        [Obsolete("Use active realms Intrinsics to access well-known object")]
        public EvalFunctionInstance Eval => Realm.Intrinsics.Eval;

        [Obsolete("Use active realms Intrinsics to access well-known object")]
        public ErrorConstructor Error => Realm.Intrinsics.Error;

        [Obsolete("Use active realms Intrinsics to access well-known object")]
        public FunctionConstructor Function => Realm.Intrinsics.Function;

        [Obsolete("Use active realms Intrinsics to access well-known object")]
        public JsonInstance Json => Realm.Intrinsics.Json;

        [Obsolete("Use active realms Intrinsics to access well-known object")]
        public ObjectConstructor Object => Realm.Intrinsics.Object;

        [Obsolete("Use active realms Intrinsics to access well-known object")]
        public StringConstructor String => Realm.Intrinsics.String;
    }
}