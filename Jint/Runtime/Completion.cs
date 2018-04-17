using System;
using System.Runtime.CompilerServices;
using Esprima;
using Jint.Native;

namespace Jint.Runtime
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.9
    /// </summary>
    public sealed class Completion
    {
        public const string Normal = "normal";
        public const string Break = "break";
        public const string Continue = "continue";
        public const string Return = "return";
        public const string Throw = "throw";

        internal static readonly Completion Empty = new Completion(Normal, null, null).Freeze();
        internal static readonly Completion EmptyUndefined = new Completion(Normal, Undefined.Instance, null).Freeze();

        private bool _frozen;

        public Completion(string type, JsValue value, string identifier, Location location = null)
        {
            Type = type;
            Value = value;
            Identifier = identifier;
            Location = location;
        }

        public string Type
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
            return Value != null ? Value : Undefined.Instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAbrupt()
        {
            return Type != Normal;
        }

        public Location Location { get; private set; }

        private Completion Freeze()
        {
            _frozen = true;
            return this;
        }

        public Completion Reassign(string type, JsValue value, string identifier, Location location = null)
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