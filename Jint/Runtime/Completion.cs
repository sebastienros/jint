using System;
using System.Runtime.CompilerServices;
using Esprima;
using Jint.Native;

namespace Jint.Runtime
{
    public enum CompletionType
    {
        Normal,
        Break,
        Continue,
        Return,
        Throw
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.9
    /// </summary>
    public sealed class Completion
    {
        internal static readonly Completion Empty = new Completion(CompletionType.Normal, null, null).Freeze();
        internal static readonly Completion EmptyUndefined = new Completion(CompletionType.Normal, Undefined.Instance, null).Freeze();

        private bool _frozen;

        public Completion(CompletionType type, JsValue value, string identifier, Location location = null)
        {
            Type = type;
            Value = value;
            Identifier = identifier;
            Location = location;
        }

        public CompletionType Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }

        public JsValue Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }

        public string Identifier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue GetValueOrDefault()
        {
            return Value ?? Undefined.Instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAbrupt()
        {
            return Type != CompletionType.Normal;
        }

        public Location Location { get; private set; }

        private Completion Freeze()
        {
            _frozen = true;
            return this;
        }

        public Completion Reassign(CompletionType type, JsValue value, string identifier, Location location = null)
        {
            if (_frozen)
            {
                throw new InvalidOperationException("object is frozen");
            }

            Type = type;
            Value = value;
            Identifier = identifier;
            Location = location;

            return this;
        }
    }
}