using Jint.Native;
using Jint.Runtime;

namespace Jint.Collections
{
    /// <summary>
    /// Helps traversing objects and checks for cyclic references.
    /// </summary>
    internal sealed class ObjectTraverseStack
    {
        private readonly Engine _engine;
        private readonly Stack<object> _stack = new();

        public ObjectTraverseStack(Engine engine)
        {
            _engine = engine;
        }

        public void Enter(JsValue value)
        {
            if (value is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(value));
            }

            if (_stack.Contains(value))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm, "Cyclic reference detected.");
            }

            _stack.Push(value);
        }

        public void Exit()
        {
            _stack.Pop();
        }
    }
}
