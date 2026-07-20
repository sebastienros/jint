var length = 0;
for (var i = 0; i < 2000; i++) {
    length += host.concat('abcdefgh', String(i)).length;
}
if (length !== 22890) {
    throw new Error('unexpected length: ' + length);
}
