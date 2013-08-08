/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-15.js
 * @description 7.3 - ES5 recognize <BOM> (\uFFFF) as a whitespace character
 */


function testcase() {
        var prop = "a\uFFFFa";
        return prop.length === 3 && prop !== "aa" && prop[1] === "\uFFFF";
    }
runTestCase(testcase);
