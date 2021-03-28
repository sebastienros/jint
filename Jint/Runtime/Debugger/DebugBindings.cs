using System;
using System.Collections;
using System.Collections.Generic;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    public sealed class DebugBinding
    {
        public string Name { get; }
        public JsValue Value { get; }

        internal DebugBinding(string name, JsValue value)
        {
            Name = name;
            Value = value;
        }
    }

    internal sealed class DebugBindings : IReadOnlyList<DebugBinding>
    {
        private class Enumerator : IEnumerator<DebugBinding>
        {
            private readonly DebugBindings _owner;
            private readonly int _count;
            private int _position = -1;
            private DebugBinding _current;

            public Enumerator(DebugBindings owner)
            {
                _owner = owner;
                _count = owner.Count;
            }

            public DebugBinding Current => _current;

            object IEnumerator.Current => _current;

            public bool MoveNext()
            {
                _position++;
                if (_position < _count)
                {
                    _current = _owner[_position];
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                _position = -1;
                _current = null;
            }

            public void Dispose()
            {

            }
        }

        private readonly EnvironmentRecord _record;
        private readonly List<string> _bindings;

        public DebugBindings(EnvironmentRecord record, List<string> bindings)
        {
            _record = record;
            _bindings = bindings;
        }

        public int Count => _bindings.Count;

        public DebugBinding this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                string name = _bindings[index];
                return new DebugBinding(name, _record.GetBindingValue(name, strict: false));
            }
        }

        public IEnumerator<DebugBinding> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
