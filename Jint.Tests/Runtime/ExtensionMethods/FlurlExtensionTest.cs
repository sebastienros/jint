using Flurl.Http;

namespace Jint.Tests.Runtime.ExtensionMethods;

public class FlurlExtensionTest
{
    [RunnableInDebugOnlyAttribute]
    public void CanUseFlurlExtensionMethods()
    {
        var engine = new Engine(options =>
        {
            options.AddExtensionMethods(
                typeof(GeneratedExtensions),
                typeof(Flurl.GeneratedExtensions));
        });

        const string script = @"
var result = 'https://httpbin.org/anything'
        .AppendPathSegment('person')
        .SetQueryParams({ a: 1, b: 2 })
        .WithOAuthBearerToken('my_oauth_token')
        .PostJsonAsync({
            first_name: 'Claire',
            last_name: 'Underwood'
         }).GetAwaiter().GetResult();
";

        engine.Execute(script);

        var result = engine.GetValue("result").ToObject();
    }
}