/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-1.js
 * @description 7.3 - ES5 recognizes the character <LS> (\u2028) as line terminators when parsing statements
 */


function testcase() {
        eval("var test7_3_1\u2028prop = 66;");
        return (prop === 66) && ((typeof test7_3_1) === "undefined");
    }
runTestCase(testcase);
