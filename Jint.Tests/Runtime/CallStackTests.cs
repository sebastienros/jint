namespace Jint.Tests.Runtime;

public class CallStackTests
{
    [Test]
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
        engine.CallStack.Count.Should().Be(0);
    }

    [Test]
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
        engine.CallStack.Count.Should().Be(0);
    }
}