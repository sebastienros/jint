using System.Diagnostics;
using System.Text.Json;
using BrowserComparison;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

if (args is ["--idle-check"])
{
 if (Environment.GetEnvironmentVariable("JINT_BENCH_SKIP_IDLE_CHECK") is "1" or "true") throw new Exception("Idle override refused");
 var errors = Jint.Benchmark.MachineStateValidator.Blocking.Validate(null!).ToArray();
 Console.WriteLine(JsonSerializer.Serialize(new { Accepted = errors.Length == 0, Errors = errors.Select(e => e.Message) }));
 return errors.Length == 0 ? 0 : 1;
}
var endpoint = args[0];
var pid = int.Parse(args[1]);
var name = args[2];
var iterations = int.Parse(args[3]);
var folder = args[4];
var definition = Workloads.Get(name);
await File.WriteAllTextAsync(Path.Combine(folder, "workload.json"), JsonSerializer.Serialize(new {
 definition.Html, definition.Script, definition.Expected,
 HtmlSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(definition.Html))),
 ScriptSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(definition.Script))) }));
var builder = WebApplication.CreateSlimBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var server = builder.Build();
server.MapGet("/data", () => Results.Json(new { values = Enumerable.Range(0,100).ToArray() }));
server.MapGet("/fixture", async context => { context.Response.ContentType = "text/html; charset=utf-8"; await context.Response.WriteAsync(definition.Html); });
await server.StartAsync();
var url = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/fixture";
using var browser = await Puppeteer.ConnectAsync(new ConnectOptions { BrowserWSEndpoint=endpoint, DefaultViewport=null });
await File.WriteAllTextAsync(Path.Combine(folder,"identity.json"), JsonSerializer.Serialize(new { Version=await browser.GetVersionAsync(), UserAgent=await browser.GetUserAgentAsync() }));
var page = await browser.NewPageAsync();
var process = Process.GetProcessById(pid);
var rows = new List<object>();
async Task Exercise(int index) {
 var start = DateTime.UtcNow;
 var cpu = process.TotalProcessorTime.TotalMilliseconds;
 var sw=Stopwatch.StartNew();
 await page.GoToAsync(url, new NavigationOptions { WaitUntil=[WaitUntilNavigation.Load], Timeout=30000 });
 var nav=sw.Elapsed.TotalMilliseconds;
 var navEnd=DateTime.UtcNow;
 var actual=await page.EvaluateExpressionAsync<string>(definition.Script).WaitAsync(TimeSpan.FromSeconds(30));
 var end=DateTime.UtcNow;
 if(actual!=definition.Expected) throw new Exception($"{name}: {actual} != {definition.Expected}");
 rows.Add(new {index,start,navEnd,end,navMs=nav,evalMs=sw.Elapsed.TotalMilliseconds-nav,cpuMs=process.TotalProcessorTime.TotalMilliseconds-cpu,actual});
}
for(int i=-(args.Length>5?int.Parse(args[5]):5);i<0;i++) await Exercise(i);
await File.WriteAllTextAsync(Path.Combine(folder,"ready"),"ready");
while(!File.Exists(Path.Combine(folder,"go"))) await Task.Delay(25);
for(int i=0;i<iterations;i++) await Exercise(i);
await File.WriteAllTextAsync(Path.Combine(folder,"rows.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
await page.CloseAsync();
browser.Disconnect();

return 0;
