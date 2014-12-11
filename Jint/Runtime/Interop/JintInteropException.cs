﻿using System;

namespace Jint.Runtime.Interop
{
    public class JintInteropException : Exception
    {
        public JintInteropException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
