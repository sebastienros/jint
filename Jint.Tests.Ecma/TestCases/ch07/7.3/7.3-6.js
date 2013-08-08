/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-6.js
 * @description 7.3 - ES5 recognizes the character <PS> (\u2029) as terminating string literal
 */


function testcase() {
        var prop = "66\u2029123";
        return prop === "66\u2029123" && prop[2] === "\u2029" && prop.length === 6;
    }
runTestCase(testcase);
