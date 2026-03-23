using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class DecodeUriBenchmark
{
    private const string HarnessScript = @"
function decimalToPercentHexString(n) {
  var hex = '0123456789ABCDEF';
  return '%' + hex[(n >> 4) & 0xf] + hex[n & 0xf];
}";

    private Prepared<Script> _decodeUriBulk;
    private Prepared<Script> _decodeUriComponentBulk;

    [GlobalSetup]
    public void Setup()
    {
        // Matches the real test262 S15.1.3.1_A2.5_T1.js pattern with function calls
        _decodeUriBulk = Engine.PrepareScript(HarnessScript + @"
for (var indexB1 = 0xF0; indexB1 <= 0xF4; indexB1++) {
  var hexB1 = decimalToPercentHexString(indexB1);
  for (var indexB2 = 0x80; indexB2 <= 0xBF; indexB2++) {
    if ((indexB1 === 0xF0) && (indexB2 <= 0x9F)) continue;
    if ((indexB1 === 0xF4) && (indexB2 >= 0x90)) continue;
    var hexB1_B2 = hexB1 + decimalToPercentHexString(indexB2);
    for (var indexB3 = 0x80; indexB3 <= 0xBF; indexB3++) {
      var hexB1_B2_B3 = hexB1_B2 + decimalToPercentHexString(indexB3);
      for (var indexB4 = 0x80; indexB4 <= 0xBF; indexB4++) {
        var hexB1_B2_B3_B4 = hexB1_B2_B3 + decimalToPercentHexString(indexB4);
        var index = (indexB1 & 0x07) * 0x40000 + (indexB2 & 0x3F) * 0x1000 + (indexB3 & 0x3F) * 0x40 + (indexB4 & 0x3F);
        var L = ((index - 0x10000) & 0x03FF) + 0xDC00;
        var H = (((index - 0x10000) >> 10) & 0x03FF) + 0xD800;
        decodeURI(hexB1_B2_B3_B4);
        String.fromCharCode(H, L);
      }
    }
  }
}");

        _decodeUriComponentBulk = Engine.PrepareScript(HarnessScript + @"
for (var indexB1 = 0xF0; indexB1 <= 0xF4; indexB1++) {
  var hexB1 = decimalToPercentHexString(indexB1);
  for (var indexB2 = 0x80; indexB2 <= 0xBF; indexB2++) {
    if ((indexB1 === 0xF0) && (indexB2 <= 0x9F)) continue;
    if ((indexB1 === 0xF4) && (indexB2 >= 0x90)) continue;
    var hexB1_B2 = hexB1 + decimalToPercentHexString(indexB2);
    for (var indexB3 = 0x80; indexB3 <= 0xBF; indexB3++) {
      var hexB1_B2_B3 = hexB1_B2 + decimalToPercentHexString(indexB3);
      for (var indexB4 = 0x80; indexB4 <= 0xBF; indexB4++) {
        var hexB1_B2_B3_B4 = hexB1_B2_B3 + decimalToPercentHexString(indexB4);
        var index = (indexB1 & 0x07) * 0x40000 + (indexB2 & 0x3F) * 0x1000 + (indexB3 & 0x3F) * 0x40 + (indexB4 & 0x3F);
        var L = ((index - 0x10000) & 0x03FF) + 0xDC00;
        var H = (((index - 0x10000) >> 10) & 0x03FF) + 0xD800;
        decodeURIComponent(hexB1_B2_B3_B4);
        String.fromCharCode(H, L);
      }
    }
  }
}");
    }

    [Benchmark]
    public void DecodeUri_Bulk()
    {
        var engine = new Engine();
        engine.Execute(_decodeUriBulk);
    }

    [Benchmark]
    public void DecodeUriComponent_Bulk()
    {
        var engine = new Engine();
        engine.Execute(_decodeUriComponentBulk);
    }
}
