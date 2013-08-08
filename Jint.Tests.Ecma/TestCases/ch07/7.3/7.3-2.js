/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-2.js
 * @description 7.3 - ES5 recognizes the character <PS> (\u2029) as line terminators when parsing statements
 */


function testcase() {
        eval("var test7_3_2\u2029prop = 66;");
        return (prop===66) && ((typeof test7_3_2) === "undefined");
    }
runTestCase(testcase);
