using System;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing;

using var trace = new EventPipeEventSource(args[0]);
trace.Process();
Console.WriteLine(JsonSerializer.Serialize(new
{
    StartUtc = trace.SessionStartTime.ToUniversalTime().ToString("O"),
    EndUtc = trace.SessionEndTime.ToUniversalTime().ToString("O"),
    trace.EventsLost
}));
