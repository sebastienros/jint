using System;

namespace Jint.Runtime
{
    public class StatementsCountOverflowException : Exception 
    {
        public Jint.Parser.Location Location;
        public StatementsCountOverflowException(Jint.Parser.Location location = null)
            : base("The maximum number of statements executed have been reached.")
        {
            InitLocation(location);
        }

        public void InitLocation(Jint.Parser.Location location)
        {
            if (location != null && !this.Location.IsInitialized)
            {
                this.Location = location.Clone();
            }
        }

        public void InitLocation(Jint.Parser.Ast.Statement statement)
        {
            if (statement != null)
            {
                InitLocation(statement.Location);
            }
        }
    }
}
