startTest("dromaeo-object-regexp", '812dde38');

// Try to force real results
let str = [], tmp, ret, re;
const testStrings = [];
var i = 65536;

const randomChar = () => String.fromCharCode((25 * Math.random()) + 97);

for (let i = 0; i < 16384; i++)
    str.push(randomChar());

str = str.join("");
str += str;
str += str;

const generateTestStrings = count => {
    let t, nest;
    if (testStrings.length >= count)
        return testStrings.slice(0, count);
    for (let i = testStrings.length; i < count; i++) {
        // Make all tested strings different
        t = randomChar() + str + randomChar();
        nest = Math.floor(4 * Math.random());
        for (let j = 0; j < nest; j++) {
            t = randomChar() + t + randomChar();
        }
        // Try to minimize benchmark order dependencies by
        // exercising the strings
        for (let j = 0; j < t.length; j += 100) {
            ret = t[j];
            ret = t.substring(j, j + 100);
        }
        testStrings[i] = t;
    }
    return testStrings;
};

// TESTS: split

prep(() => {
    // It's impossible to specify empty regexp by simply
    // using two slashes as this will be interpreted as a
    // comment start. See note to ECMA-262 5th 7.8.5.
    re = /(?:)/;
    tmp = generateTestStrings(30);
});

test("Compiled Object Empty Split", () => {
    for (let i = 0; i < 30; i++)
        ret = tmp[i].split(re);
});

prep(() => {
    re = /a/;
    tmp = generateTestStrings(30);
});

test("Compiled Object Char Split", () => {
    for (let i = 0; i < 30; i++)
        ret = tmp[i].split(re);
});

prep(() => {
    re = /.*/;
    tmp = generateTestStrings(100);
});

test("Compiled Object Variable Split", () => {
    for (let i = 0; i < 100; i++)
        ret = tmp[i].split(re);
});

// TESTS: Compiled RegExps

prep(() => {
    re = /aaaaaaaaaa/g;
    tmp = generateTestStrings(100);
});

test("Compiled Match", () => {
    for (let i = 0; i < 100; i++)
        ret = tmp[i].match(re);
});

prep(() => {
    tmp = generateTestStrings(100);
});

test("Compiled Test", () => {
    for (let i = 0; i < 100; i++)
        ret = re.test(tmp[i]);
});

prep(() => {
    tmp = generateTestStrings(100);
});

test("Compiled Empty Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled 12 Char Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "asdfasdfasdf");
});

prep(() => {
    re = new RegExp("aaaaaaaaaa", "g");
    tmp = generateTestStrings(100);
});

test("Compiled Object Match", () => {
    for (let i = 0; i < 100; i++)
        ret = tmp[i].match(re);
});

prep(() => {
    tmp = generateTestStrings(100);
});

test("Compiled Object Test", () => {
    for (let i = 0; i < 100; i++)
        ret = re.test(tmp[i]);
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Object Empty Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Object 12 Char Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "asdfasdfasdf");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Object 12 Char Replace Function", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, all => "asdfasdfasdf");
});

// TESTS: Variable Length

prep(() => {
    re = /a.*a/;
    tmp = generateTestStrings(100);
});

test("Compiled Variable Match", () => {
    for (let i = 0; i < 100; i++)
        ret = tmp[i].match(re);
});

prep(() => {
    tmp = generateTestStrings(100);
});

test("Compiled Variable Test", () => {
    for (let i = 0; i < 100; i++)
        ret = re.test(tmp[i]);
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Variable Empty Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Variable 12 Char Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "asdfasdfasdf");
});

prep(() => {
    re = new RegExp("aaaaaaaaaa", "g");
    tmp = generateTestStrings(100);
});

test("Compiled Variable Object Match", () => {
    for (let i = 0; i < 100; i++)
        ret = tmp[i].match(re);
});

prep(() => {
    tmp = generateTestStrings(100);
});

test("Compiled Variable Object Test", () => {
    for (let i = 0; i < 100; i++)
        ret = re.test(tmp[i]);
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Variable Object Empty Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Variable Object 12 Char Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "asdfasdfasdf");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Variable Object 12 Char Replace Function", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, all => "asdfasdfasdf");
});

// TESTS: Capturing

prep(() => {
    re = /aa(b)aa/g;
    tmp = generateTestStrings(100);
});

test("Compiled Capture Match", () => {
    for (let i = 0; i < 100; i++)
        ret = tmp[i].match(re);
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Capture Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "asdfasdfasdf");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Capture Replace with Capture", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, "asdf\\1asdfasdf");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Capture Replace with Capture Function", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, (all, capture) => "asdf" + capture + "asdfasdf");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Compiled Capture Replace with Upperase Capture Function", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(re, (all, capture) => capture.toUpperCase());
});

// TESTS: Uncompiled RegExps

prep(() => {
    tmp = generateTestStrings(100);
});

test("Uncompiled Match", () => {
    for (let i = 0; i < 100; i++)
        ret = tmp[i].match(/aaaaaaaaaa/g);
});

prep(() => {
    tmp = generateTestStrings(100);
});

test("Uncompiled Test", () => {
    for (let i = 0; i < 100; i++)
        ret = (/aaaaaaaaaa/g).test(tmp[i]);
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Uncompiled Empty Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(/aaaaaaaaaa/g, "");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Uncompiled 12 Char Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(/aaaaaaaaaa/g, "asdfasdfasdf");
});

prep(() => {
    tmp = generateTestStrings(100);
});

test("Uncompiled Object Match", () => {
    for (let i = 0; i < 100; i++)
        ret = tmp[i].match(new RegExp("aaaaaaaaaa", "g"));
});

prep(() => {
    tmp = generateTestStrings(100);
});

test("Uncompiled Object Test", () => {
    for (let i = 0; i < 100; i++)
        ret = (new RegExp("aaaaaaaaaa", "g")).test(tmp[i]);
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Uncompiled Object Empty Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(new RegExp("aaaaaaaaaa", "g"), "");
});

prep(() => {
    tmp = generateTestStrings(50);
});

test("Uncompiled Object 12 Char Replace", () => {
    for (let i = 0; i < 50; i++)
        ret = tmp[i].replace(new RegExp("aaaaaaaaaa", "g"), "asdfasdfasdf");
});

endTest();