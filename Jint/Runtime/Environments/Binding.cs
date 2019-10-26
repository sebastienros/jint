﻿using Jint.Native;

namespace Jint.Runtime.Environments
{
    public struct Binding
    {
        public Binding(bool canBeDeleted, bool mutable, bool strict)
        {

            CanBeDeleted = canBeDeleted;
            Mutable = mutable;
            Strict = strict;
            Value = JsValue.Null;
        }

        public JsValue Value;
        public readonly bool CanBeDeleted;
        public readonly bool Mutable;
        public readonly bool Strict;
    }
}
