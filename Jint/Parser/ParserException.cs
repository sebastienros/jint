using System;

namespace Jint.Parser
{
    public class Error : Exception
    {
        public int Column;
        public string Description;
        public int Index;
        public int LineNumber;

        public Error(string message) : base(message)
        {
        }
    }
}