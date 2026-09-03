using System.Text;
using Jint.Browser.Tool;

// Standard output is opened here rather than taken from Console, so that a document written to a pipe is
// UTF-8 with no byte order mark on every platform: Console.Out on Windows is the console code page, and a
// mark at the front of a markdown file is a character every reader of it then carries.
var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
{
    AutoFlush = true,
};

var error = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
{
    AutoFlush = true,
};

using var stopping = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    // Cancel the default kill so that `serve` can dispose its pages and its server rather than being torn
    // out from under them. A second Ctrl+C is the operating system's, as it should be.
    e.Cancel = !stopping.IsCancellationRequested;
    stopping.Cancel();
};

return await ToolProgram.RunAsync(args, output, error, stopping.Token).ConfigureAwait(false);
