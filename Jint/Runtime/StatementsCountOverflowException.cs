namespace Jint.Runtime
{
    public class StatementsCountOverflowException : JintException 
    {
        public StatementsCountOverflowException() : base("The maximum number of statements executed have been reached.")
        {
        }
    }
}
