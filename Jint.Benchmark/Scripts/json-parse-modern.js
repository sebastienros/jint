let payload = (() => {
    let seed = 20260711;
    const next = () => { seed = (seed * 1664525 + 1013904223) | 0; return seed >>> 8; };
    const tags = ['alpha', 'beta', 'gamma', 'delta', 'epsilon', 'zeta', 'eta', 'theta'];
    const records = [];
    for (let i = 0; i < 500; i++) {
        records.push({
            id: i,
            name: `user${next() % 10000}`,
            active: (next() & 1) === 1,
            score: (next() % 10000) / 100,
            tags: [tags[next() & 7], tags[next() & 7]],
            ts: 1700000000 + i
        });
    }
    return JSON.stringify(records);
})();

let checksum = 0;
for (let iter = 0; iter < 32; iter++) {
    const parsed = JSON.parse(payload);
    let total = 0;
    for (const r of parsed) {
        total += r.id + r.score + (r.active ? 1 : 0) + r.tags.length + r.name.length;
    }
    checksum += total;
    if ((iter & 7) === 7) {
        payload = JSON.stringify(parsed);
    }
}
if (!(checksum > 0)) {
    throw new Error(`json-parse checksum mismatch: ${checksum}`);
}
