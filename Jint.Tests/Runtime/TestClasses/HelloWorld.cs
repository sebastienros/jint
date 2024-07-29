namespace Jint.Tests.Runtime.TestClasses;

public class HelloWorld
{
    public void ThrowException()
    {
        int zero = 0;
        int x = 5 / zero;
    }
}