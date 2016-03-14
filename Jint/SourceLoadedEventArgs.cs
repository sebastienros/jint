using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jint.Parser;

namespace Jint
{
    public class SourceLoadedEventArgs : EventArgs
    {
        public Script Source { get; }

        public SourceLoadedEventArgs(Script source)
        {
            Source = source;
        }
    }

    public delegate void SourceLoadedEventHandler(object sender, SourceLoadedEventArgs e);
}
