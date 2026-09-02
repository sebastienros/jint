// The script the Jint REPL runs under --inspect while the smoke test drives DevTools against it.
//
// Three things have to be true of it and nothing else here is deliberate:
//
//   * it logs a string *and* an object, so the Console panel has an object preview to render rather than
//     a bare string;
//   * it declares named functions, so the Sources panel has recognisable text to show and the editor's
//     gutter has a line worth putting a breakpoint on;
//   * a timer throws, so an uncaught exception is reachable.
//
// The last one is why the failing timer re-arms instead of firing once. The script itself finishes in
// milliseconds and the frontend needs seconds to download and connect, so a single `setTimeout` would have
// thrown before anybody was listening; the heartbeat gives every step after the connection something to
// catch. The heartbeat is also what makes a breakpoint hittable: `computeTotal` is called once per second
// forever, so the frontend can set a breakpoint at any time and be paused within a second.

var orders = [
    { sku: 'widget', quantity: 3, price: 4.5 },
    { sku: 'gasket', quantity: 1, price: 12 },
    { sku: 'flange', quantity: 7, price: 0.75 },
];

function lineTotal(order) {
    return order.quantity * order.price;
}

function computeTotal(items) {
    var total = 0;
    for (var i = 0; i < items.length; i++) {
        total += lineTotal(items[i]);
    }
    return total;
}

function describeOrders(items) {
    return { count: items.length, total: computeTotal(items), first: items[0].sku };
}

console.log('order book ready', describeOrders(orders));

var ticks = 0;

setInterval(function heartbeat() {
    ticks++;
    console.log('heartbeat', { tick: ticks, total: computeTotal(orders) });

    setTimeout(function reconcile() {
        throw new Error('reconciliation failed on tick ' + ticks);
    }, 50);
}, 1000);
