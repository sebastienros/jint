using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/#sec-function-environment-records
    /// </summary>
    internal sealed class FunctionEnvironmentRecord : DeclarativeEnvironmentRecord
    {
        private enum ThisBindingStatus
        {
            Lexical,
            Initialized,
            Uninitialized
        }

        private JsValue _thisValue;
        private ThisBindingStatus _thisBindingStatus;
        private readonly FunctionInstance _functionObject;
        private readonly JsValue _homeObject = Undefined;
        private readonly JsValue _newTarget;

        public FunctionEnvironmentRecord(
            Engine engine, 
            FunctionInstance functionObject,
            JsValue newTarget) : base(engine)
        {
            _functionObject = functionObject;
            _newTarget = newTarget;
            if (functionObject is ArrowFunctionInstance)
            {
                _thisBindingStatus = ThisBindingStatus.Lexical;
            }
            else
            {
                _thisBindingStatus = ThisBindingStatus.Uninitialized;
            }
        }


        public override bool HasThisBinding() => _thisBindingStatus != ThisBindingStatus.Lexical;

        public override bool HasSuperBinding() => 
            _thisBindingStatus != ThisBindingStatus.Lexical && !_homeObject.IsUndefined();

        public override JsValue WithBaseObject()
        {
            return _thisBindingStatus == ThisBindingStatus.Uninitialized
                ? ExceptionHelper.ThrowReferenceError<JsValue>(_engine)
                : _thisValue;
        }

        public JsValue BindThisValue(JsValue value)
        {
            if (_thisBindingStatus == ThisBindingStatus.Initialized)
            {
                ExceptionHelper.ThrowReferenceError<JsValue>(_engine);
            }
            _thisValue = value;
            _thisBindingStatus = ThisBindingStatus.Initialized;
            return value;
        }

        public override JsValue GetThisBinding()
        {
            return _thisBindingStatus == ThisBindingStatus.Uninitialized 
                ? ExceptionHelper.ThrowReferenceError<JsValue>(_engine)
                : _thisValue;
        }

        public JsValue GetSuperBase()
        {
            return _homeObject.IsUndefined() 
                ? Undefined
                : ((ObjectInstance) _homeObject).Prototype;
        }
    }
}
