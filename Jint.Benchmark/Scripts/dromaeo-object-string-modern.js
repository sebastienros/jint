startTest("dromaeo-object-string", 'ef8605c3');

// Try to force real results
let ret;
const num = 5000;

// TESTS: String concatenation

test("Concat String", () => {
    let str = "";
    for (let i = 0; i < num; i++)
        str += "a";
    ret = str;
});

test("Concat String Object", () => {
    let str = new String();
    for (let i = 0; i < num; i++)
        str += "a";
    ret = str;
});

test("Concat String from charCode", () => {
    let str = "";
    for (let i = 0; i < num / 2; i++)
        str += String.fromCharCode(97);
    ret = str;
});

test("Array String Join", () => {
    const str = [];
    for (let i = 0; i < num / 2; i++)
        str.push("a");
    ret = str.join("");
});

let ostr = [], tmp, tmp2, tmpstr;

for (let i = 0; i < 16384; i++)
    ostr.push(String.fromCharCode((25 * Math.random()) + 97));

ostr = ostr.join("");
ostr += ostr;
ostr += ostr;

let str;
let i = 52288;

prep(() => str = new String(ostr));

// TESTS: split
test("String Split", () => ret = str.split(""));

prep(() => {
    tmpstr = str;
    tmpstr += tmpstr;
    tmpstr += tmpstr;
    tmpstr += tmpstr;
    tmpstr += tmpstr;
});

test("String Split on Char", () => ret = tmpstr.split("a"));

prep(() => str += str);

// TESTS: characters

test("charAt", () => {
    for (let j = 0; j < num; j++) {
        ret = str.charAt(0);
        ret = str.charAt(str.length - 1);
        ret = str.charAt(15000);
        ret = str.charAt(12000);
    }
});

test("[Number]", () => {
    for (let j = 0; j < num; j++) {
        ret = str[0];
        ret = str[str.length - 1];
        ret = str[15000];
        ret = str[10000];
        ret = str[5000];
    }
});

test("charCodeAt", () => {
    for (let j = 0; j < num; j++) {
        ret = str.charCodeAt(0);
        ret = str.charCodeAt(str.length - 1);
        ret = str.charCodeAt(15000);
        ret = str.charCodeAt(10000);
        ret = str.charCodeAt(5000);
    }
});

// TESTS: indexOf

test("indexOf", () => {
    for (let j = 0; j < num; j++) {
        ret = str.indexOf("a");
        ret = str.indexOf("b");
        ret = str.indexOf("c");
        ret = str.indexOf("d");
    }
});

test("lastIndexOf", () => {
    for (let j = 0; j < num; j++) {
        ret = str.lastIndexOf("a");
        ret = str.lastIndexOf("b");
        ret = str.lastIndexOf("c");
        ret = str.lastIndexOf("d");
    }
});

// TESTS: slice

test("slice", () => {
    for (let j = 0; j < num; j++) {
        ret = str.slice(0);
        ret = str.slice(0, 5);
        ret = str.slice(-1);
        ret = str.slice(-6, -1);
        ret = str.slice(15000, 15005);
        ret = str.slice(12000, -1);
    }
});

// TESTS: substr

test("substr", () => {
    for (let j = 0; j < num; j++) {
        ret = str.substr(0);
        ret = str.substr(0, 5);
        ret = str.substr(-1);
        ret = str.substr(-6, 1);
        ret = str.substr(15000, 5);
        ret = str.substr(12000, 5);
    }
});

// TESTS: substring

test("substring", () => {
    for (let j = 0; j < num; j++) {
        ret = str.substring(0);
        ret = str.substring(0, 5);
        ret = str.substring(-1);
        ret = str.substring(-6, -1);
        ret = str.substring(15000, 15005);
        ret = str.substring(12000, -1);
    }
});

// TESTS: toLower/UpperCase

test("toLowerCase", () => {
    for (let j = 0; j < num / 1000; j++) {
        ret = str.toLowerCase();
    }
});

test("toUpperCase", () => {
    for (let j = 0; j < num / 1000; j++) {
        ret = str.toUpperCase();
    }
});

// TESTS: comparing
prep(() => {
    tmp = str;
    tmp2 = str;
});

test("comparing", () => {
    tmp = "a" + tmp + "a";
    tmp2 = "a" + tmp2 + "a";
    for (let j = 0; j < num / 1000; j++) {
        ret = tmp == tmp2;
        ret = tmp < tmp2;
        ret = tmp > tmp2;
    }
});

endTest();