namespace Jint.Tests.Runtime;

public class CallStackTests
{
    [Fact]
    public void ShouldUnwindAfterCaughtException()
    {
        var engine = new Engine();
        engine.Execute(@"
                function thrower()
                {
                    throw new Error('test');
                }

                try
                {
                    thrower();
                }
                catch (error)
                {
                }
                "
        );
        Assert.Equal(0, engine.CallStack.Count);
    }

    [Fact]
    public void ShouldUnwindAfterCaughtExceptionNested()
    {
        var engine = new Engine();
        engine.Execute(@"
                function thrower2()
                {
                    throw new Error('test');
                }

                function thrower1()
                {
                    thrower2();
                }

                try
                {
                    thrower1();
                }
                catch (error)
                {
                }
            ");
        Assert.Equal(0, engine.CallStack.Count);
    }
}