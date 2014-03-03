using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Runtime.Interop
{
    public interface IObjectWrapper
    {
        object Target { get; }
    }
}
