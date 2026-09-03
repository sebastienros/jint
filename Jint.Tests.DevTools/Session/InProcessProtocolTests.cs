using System.Text.Json;
using Jint.DevTools.Protocol;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// The protocol as a client sees it: JSON in, JSON out, over a connection with no socket under it.
/// </summary>
/// <remarks>
/// These are the tests that pin the envelope and the error codes, and they are deliberately written against
/// text rather than against the domain objects. A client library matches on <c>id</c>, <c>error.code</c> and
/// — for a method it is feature-detecting — on <c>error.message</c>, so those are what has to be asserted;
/// a test calling the domain directly would pass with the envelope broken.
/// </remarks>
public class InProcessProtocolTests
{
    [Test]
    public async Task SchemaGetDomainsAnswersTheManifest()
    {
        await using var session = ProtocolSession.Create();
        var result = await session.ResultOfAsync("""{"id":1,"method":"Schema.getDomains"}""");

        var domains = result.GetProperty("domains").EnumerateArray()
            .Select(domain => domain.GetProperty("name").GetString())
            .ToArray();

        domains.Should().BeEquivalentTo(["Browser", "Console", "Debugger", "Log", "Profiler", "Runtime", "Schema", "Target"]);
        result.GetProperty("domains")[0].GetProperty("version").GetString().Should().Be("1.3");
    }

    [Test]
    public async Task BrowserGetVersionNamesJintRatherThanAChromeBuild()
    {
        await using var session = ProtocolSession.Create();
        var result = await session.ResultOfAsync("""{"id":7,"method":"Browser.getVersion"}""");

        result.GetProperty("protocolVersion").GetString().Should().Be("1.3");
        result.GetProperty("product").GetString().Should().StartWith("Jint/");
        result.GetProperty("userAgent").GetString().Should().StartWith("Jint/");
        result.GetProperty("revision").GetString().Should().BeEmpty();
        result.GetProperty("jsVersion").GetString().Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task BrowserCloseReachesTheHostAndAnswersAnEmptyResult()
    {
        var closed = 0;
        await using var session = ProtocolSession.Create(() => closed++);
        var result = await session.ResultOfAsync("""{"id":2,"method":"Browser.close"}""");

        closed.Should().Be(1);
        result.ValueKind.Should().Be(JsonValueKind.Object);
        result.GetRawText().Should().Be("{}", "a command that returns nothing answers with an empty result object, not with null");
    }

    [Test]
    public async Task BrowserCloseWithoutAHostCallbackStillSucceeds()
    {
        await using var session = ProtocolSession.Create();
        var result = await session.ResultOfAsync("""{"id":2,"method":"Browser.close"}""");

        result.GetRawText().Should().Be("{}");
    }

    [Test]
    public async Task TheResponseEchoesTheRequestIdentifier()
    {
        await using var session = ProtocolSession.Create();
        var reply = await session.RoundTripAsync("""{"id":4294967296,"method":"Browser.getVersion"}""");

        reply.GetProperty("id").GetInt64().Should().Be(4294967296, "identifiers are 64-bit, and a client that ran long enough to overflow an int is one that gets wrong answers");
    }

    /// <summary>
    /// A <c>sessionId</c> nothing answers to is <c>-32001</c> in Chrome's wording, and the failure still
    /// echoes the identifier so the client can tell which of its attachments went away.
    /// </summary>
    [Test]
    public async Task AMessageNamingAnUnknownSessionIsSessionNotFound()
    {
        await using var session = ProtocolSession.Create();
        var reply = await session.RoundTripAsync("""{"id":1,"method":"Browser.getVersion","sessionId":"AB12"}""");

        reply.GetProperty("sessionId").GetString().Should().Be("AB12");
        reply.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32001);
        reply.GetProperty("error").GetProperty("message").GetString().Should().Be("Session with given id not found.");
    }

    [Test]
    public async Task AResponseWithoutASessionIdentifierCarriesNone()
    {
        await using var session = ProtocolSession.Create();
        var reply = await session.RoundTripAsync("""{"id":1,"method":"Browser.getVersion"}""");

        reply.TryGetProperty("sessionId", out _).Should().BeFalse();
    }

    /// <summary>
    /// The one error message a client actually reads: Puppeteer and the DevTools front end both probe for a
    /// domain by sending one of its commands and looking at what comes back.
    /// </summary>
    [TestCase("Runtime.evaluate", TestName = "A generated but unimplemented command")]
    [TestCase("Debugger.enable", TestName = "A generated but unimplemented command of another domain")]
    [TestCase("Browser.getHistogram", TestName = "An unimplemented command of an implemented domain")]
    [TestCase("Page.navigate", TestName = "A command of a domain that is not generated at all")]
    [TestCase("Browser.thereIsNoSuchCommand", TestName = "A command the protocol does not declare")]
    [TestCase("nodot", TestName = "A method that is not qualified at all")]
    public async Task AnUnansweredMethodIsMethodNotFoundInChromesWording(string method)
    {
        await using var session = ProtocolSession.Create();
        var error = await session.ErrorOfAsync($$"""{"id":11,"method":"{{method}}"}""");

        error.GetProperty("code").GetInt32().Should().Be(-32601);
        error.GetProperty("message").GetString().Should().Be($"'{method}' wasn't found");
    }

    /// <summary>
    /// A command its domain's manifest entry does not generate answers exactly what it answered when the
    /// domain was generated whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The manifest can name the part of a domain to generate, which is how <c>Audits</c> stopped costing
    /// 143 KB of data transfer objects for an <c>enable</c> and a <c>disable</c>
    /// (<see href="https://github.com/sebastienros/jint/issues/3683">#3683</see>). A command left out has no
    /// virtual on the generated dispatch base, so it is answered by that base's <c>default</c> case rather
    /// than by a virtual whose body is the same refusal - and this is the test that says the client cannot
    /// tell, which is the whole claim the change rests on.
    /// </para>
    /// <para>
    /// <c>Browser</c> because it is engine-level and registered on this conversation: its refusal comes from
    /// the generated <c>BrowserDomainBase</c>, where a page-level domain's would come from the session
    /// having no such domain at all and would prove nothing about the dispatch.
    /// </para>
    /// </remarks>
    [TestCase("Browser.getHistograms")]
    [TestCase("Browser.setPermission")]
    [TestCase("Browser.setWindowBounds", TestName = "A command it does generate is not refused this way")]
    public async Task ACommandAPartialDomainDoesNotGenerateIsStillMethodNotFound(string method)
    {
        await using var session = ProtocolSession.Create();
        var reply = await session.SendAsync(method, "{}");

        if (string.Equals(method, "Browser.setWindowBounds", StringComparison.Ordinal))
        {
            // Generated and implemented, so whatever it answers is not "wasn't found".
            if (reply.TryGetProperty("error", out var refused))
            {
                refused.GetProperty("message").GetString().Should().NotContain("wasn't found");
            }

            return;
        }

        var error = reply.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32601);
        error.GetProperty("message").GetString().Should().Be($"'{method}' wasn't found");
    }

    /// <summary>
    /// The order the two failures are decided in, which a client can tell apart and would be misled by:
    /// <c>-32602</c> says "you called it wrongly" about a command that does not exist here at all.
    /// </summary>
    [Test]
    public async Task AnUnansweredMethodIsMethodNotFoundEvenWhenItsParametersAreUnusable()
    {
        await using var session = ProtocolSession.Create();
        var error = await session.ErrorOfAsync(
            """{"id":5,"method":"Browser.getHistogram","params":{"name":42}}""");

        error.GetProperty("code").GetInt32().Should().Be(
            -32601,
            "a command the server does not implement is not in its dispatch table, so its parameters are never read");
    }

    [Test]
    public async Task AnErrorResponseCarriesTheRequestIdentifier()
    {
        await using var session = ProtocolSession.Create();
        var reply = await session.RoundTripAsync("""{"id":99,"method":"Runtime.evaluate"}""");

        reply.GetProperty("id").GetInt64().Should().Be(99, "a client is waiting on that identifier, and an error it cannot match up is a hang");
    }

    [Test]
    public async Task MalformedJsonIsAParseError()
    {
        await using var session = ProtocolSession.Create();
        var error = await session.ErrorOfAsync("{\"id\":1,\"method\":");

        error.GetProperty("code").GetInt32().Should().Be(-32700);
        error.GetProperty("message").GetString().Should().Be("Message must be a valid JSON");
    }

    [Test]
    public async Task AMessageWithNoIdentifierIsAnErrorNotification()
    {
        await using var session = ProtocolSession.Create();
        var reply = await session.RoundTripAsync("""{"method":"Browser.getVersion"}""");

        reply.TryGetProperty("id", out _).Should().BeFalse("there is no identifier to address the failure to, so Chrome sends a notification rather than a response");
        reply.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32600);
        reply.GetProperty("error").GetProperty("message").GetString().Should().Be("Message must have integer 'id' property");
    }

    [TestCase("""{"id":"1","method":"Browser.getVersion"}""", TestName = "A string identifier")]
    [TestCase("""{"id":1.5,"method":"Browser.getVersion"}""", TestName = "A fractional identifier")]
    [TestCase("[]", TestName = "A message that is not an object")]
    public async Task AMessageThatIsNotARequestIsAnInvalidRequest(string message)
    {
        await using var session = ProtocolSession.Create();
        var error = await session.ErrorOfAsync(message);

        error.GetProperty("code").GetInt32().Should().Be(-32600);
    }

    [Test]
    public async Task AMessageWithoutAMethodIsAnInvalidRequest()
    {
        await using var session = ProtocolSession.Create();
        var reply = await session.RoundTripAsync("""{"id":3}""");

        reply.GetProperty("id").GetInt64().Should().Be(3);
        reply.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32600);
        reply.GetProperty("error").GetProperty("message").GetString().Should().Be("Message must have string 'method' property");
    }

    [Test]
    public async Task NonObjectParametersAreAnInvalidRequest()
    {
        await using var session = ProtocolSession.Create();
        var error = await session.ErrorOfAsync("""{"id":3,"method":"Browser.getVersion","params":[1,2]}""");

        error.GetProperty("code").GetInt32().Should().Be(-32600);
        error.GetProperty("message").GetString().Should().Be("Message has property 'params' of type other than object");
    }

    [Test]
    public async Task ParametersACommandDoesNotDeclareAreIgnored()
    {
        await using var session = ProtocolSession.Create();
        var result = await session.ResultOfAsync("""{"id":3,"method":"Browser.getVersion","params":{"whatIsThis":true}}""");

        result.GetProperty("protocolVersion").GetString().Should().Be("1.3");
    }

    /// <summary>
    /// A wrongly typed parameter is <c>-32602</c> and not <c>-32700</c>: the message parsed fine, and it is
    /// the payload the command refused. Told apart because the two failures are diagnosed differently — one
    /// is the client's serializer, the other its call.
    /// </summary>
    [Test]
    public void WronglyTypedParametersAreInvalidParameters()
    {
        // Neither implemented command declares a parameter, so the reader every generated command shares is
        // exercised against a payload type that does: Schema.Domain has two required strings.
        using var document = JsonDocument.Parse("""{"name":42,"version":"1.3"}""");
        var parameters = document.RootElement;

        var thrown = Assert.Throws<ProtocolException>(() =>
            ProtocolPayload.Read(parameters, ProtocolJsonContext.Default.SchemaDomain));

        thrown!.Code.Should().Be(-32602);
        thrown.Message.Should().Be("Invalid parameters");
        thrown.Details.Should().NotBeNullOrEmpty("the client author needs to know which member was wrong");
    }

    [Test]
    public void AMissingRequiredParameterIsInvalidParameters()
    {
        var thrown = Assert.Throws<ProtocolException>(() =>
            ProtocolPayload.Read(parameters: null, ProtocolJsonContext.Default.SchemaDomain));

        thrown!.Code.Should().Be(-32602);
    }

    [Test]
    public async Task EachMessageIsAnsweredOnceAndInOrder()
    {
        await using var session = ProtocolSession.Create();

        await session.RoundTripAsync("""{"id":1,"method":"Browser.getVersion"}""");
        await session.RoundTripAsync("""{"id":2,"method":"Schema.getDomains"}""");
        await session.RoundTripAsync("""{"id":3,"method":"Runtime.evaluate"}""");

        session.Sent.Should().HaveCount(3);
        session.Sent.Select(Identifier).Should().Equal(1L, 2L, 3L);
    }

    private static long Identifier(string message)
    {
        using var document = JsonDocument.Parse(message);
        return document.RootElement.GetProperty("id").GetInt64();
    }
}
