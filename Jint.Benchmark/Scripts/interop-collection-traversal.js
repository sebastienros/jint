// Host arrays do not expose a JavaScript "length" in every engine (ClearScript surfaces the CLR
// member instead), so the element count is read from a dedicated host property.
var sum = 0;
var count = host.count;
for (var pass = 0; pass < 100; pass++) {
    for (var i = 0; i < count; i++) {
        sum += host.numbers[i];
    }
}
if (sum !== 495000) {
    throw new Error('unexpected sum: ' + sum);
}
