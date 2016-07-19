using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Runtime.Descriptors.Specialized
{
    public interface ISharedDescriptor
    {
        void SetTarget(object target);
    }
}
