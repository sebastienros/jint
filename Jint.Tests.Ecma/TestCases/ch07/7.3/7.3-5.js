/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-5.js
 * @description 7.3 - ES5 recognizes the character <LS> (\u2028) as terminating string literal
 */


function testcase() {
        var prop = "66\u2028123";
        return prop === "66\u2028123" && prop[2] === "\u2028" && prop.length === 6;
    }
runTestCase(testcase);
