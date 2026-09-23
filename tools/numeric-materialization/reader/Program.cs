using System.Text.Json;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
var folder=args[0];
var rows=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"rows.json"))).RootElement.EnumerateArray().Where(r=>r.GetProperty("index").GetInt32()>=0).Select(r=>(Start:r.GetProperty("start").GetDateTime(),Nav:r.GetProperty("navEnd").GetDateTime(),End:r.GetProperty("end").GetDateTime())).ToArray();
var path=Path.Combine(folder,"profile.nettrace.etlx");
if(!File.Exists(path))path=TraceLog.CreateFromEventPipeDataFile(Path.Combine(folder,"profile.nettrace"));
using var log=new TraceLog(path);
var stacks=new Dictionary<string,long>();
var allocations=new Dictionary<string,long>();
var events=new Dictionary<string,long>();
var details=new Dictionary<string,string>();
var phaseEvents=new Dictionary<string,long>();
var gc=new List<object>();
foreach(var e in log.Events){
 var utc=e.TimeStamp.ToUniversalTime();
 var row=Array.FindIndex(rows,r=>utc>=r.Start&&utc<=r.End);
 if(row<0 && !(args.Length>1 && utc<rows[0].Start))continue;
 var phase=row<0?"startup/preparation":utc<=rows[row].Nav?"navigation":"evaluation";
 var key=e.ProviderName+"/"+e.EventName;
 events[key]=events.GetValueOrDefault(key)+1;
phaseEvents[phase+"|"+key]=phaseEvents.GetValueOrDefault(phase+"|"+key)+1;
 if(!details.ContainsKey(key) && !e.EventName.Contains("Bulk"))details[key]=string.Join("; ",e.PayloadNames.Select(n=>n+"="+e.PayloadByName(n)));
 if(e.ProviderName=="Microsoft-DotNETCore-SampleProfiler" || e is GCAllocationTickTraceData){
  var frames=new List<string>();
  for(var s=e.CallStack();s!=null;s=s.Caller)frames.Add(s.CodeAddress.FullMethodName.Length>0?s.CodeAddress.FullMethodName.Split('(')[0]:s.CodeAddress.ModuleName+"!?");
  var stack=string.Join(" <- ",frames);
  if(e is GCAllocationTickTraceData a){var ak=phase+"|"+a.TypeName+"|"+stack;allocations[ak]=allocations.GetValueOrDefault(ak)+(long)a.AllocationAmount64;}
  else {var sk=phase+"|"+e.PayloadString(0)+"|"+stack;stacks[sk]=stacks.GetValueOrDefault(sk)+1;}
 }
 if(e.EventName is "GC/Start" or "GC/Stop" or "GC/SuspendEEStart" or "GC/SuspendEEStop" or "GC/RestartEEStart" or "GC/RestartEEStop")gc.Add(new {ms=e.TimeStampRelativeMSec,e.EventName,phase,payload=string.Join("; ",e.PayloadNames.Select(n=>n+"="+e.PayloadByName(n)))});
}
using(var output=File.Create(Path.Combine(folder,"summary.json"))) JsonSerializer.Serialize(output,new{log.EventsLost,log.EventCount,events,phaseEvents,details,stacks,allocations,gc},new JsonSerializerOptions{WriteIndented=true});
Console.WriteLine($"{folder}: {stacks.Values.Sum()} samples, {allocations.Values.Sum()} allocation bytes, {log.EventsLost} lost events");
