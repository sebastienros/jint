var sum = 0;
for (var i = 0; i < 10000; i++) {
    host.value = i;
    sum += host.value;
}
if (sum !== 49995000) {
    throw new Error('unexpected sum: ' + sum);
}
