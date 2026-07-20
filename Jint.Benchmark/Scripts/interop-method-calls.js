var sum = 0;
for (var i = 0; i < 10000; i++) {
    sum += host.add(i, 1);
}
if (sum !== 50005000) {
    throw new Error('unexpected sum: ' + sum);
}
