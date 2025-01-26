startTest("dromaeo-object-array", 'bde4f5f4');

let ret = [], tmp;
const num = 500;
const i = 1024;

// TESTS: Array Building

test("Array Construction, []", () => {
    for (let j = 0; j < i * 15; j++) {
        ret = [];
        ret.length = i;
    }
});

test("Array Construction, new Array()", () => {
    for (let j = 0; j < i * 10; j++)
        ret = new Array(i);
});

test("Array Construction, unshift", () => {
    ret = [];
    for (let j = 0; j < i; j++)
        ret.unshift(j);
});

test("Array Construction, splice", () => {
    ret = [];
    for (let j = 0; j < i; j++)
        ret.splice(0, 0, j);
});

test("Array Deconstruction, shift", () => {
    const a = ret.slice();
    for (let j = 0; j < i; j++)
        tmp = a.shift();
});

test("Array Deconstruction, splice", () => {
    const a = ret.slice();
    for (let j = 0; j < i; j++)
        tmp = a.splice(0, 1);
});

test("Array Construction, push", () => {
    ret = [];
    for (let j = 0; j < i * 25; j++)
        ret.push(j);
});

test("Array Deconstruction, pop", () => {
    const a = ret.slice();
    for (let j = 0; j < i * 25; j++)
        tmp = a.pop();
});

endTest();