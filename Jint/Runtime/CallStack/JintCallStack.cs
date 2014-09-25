namespace Jint.Runtime.CallStack
{
    using System.Collections.Generic;

    public class JintCallStack
    {
        private Stack<CallStackElement> _stack = new Stack<CallStackElement>();

        private Dictionary<CallStackElement, int> _statistics =
            new Dictionary<CallStackElement, int>(new CallStackElementComparer());

        public int Push(CallStackElement item)
        {
            _stack.Push(item);
            if (_statistics.ContainsKey(item))
            {
                return ++_statistics[item];
            }
            else
            {
                _statistics.Add(item, 0);
                return 0;
            }
        }

        public void Pop()
        {
            var item = _stack.Pop();
            if (_statistics[item] == 0)
            {
                _statistics.Remove(item);
            }
            else
            {
                _statistics[item]--;
            }
        }

        // TODO printing Call Stack might become useful for debugging purposes
    }
}
