/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1.2/7.6.1-24-s.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: \u0070\u0072\u006f\u0074\u0065\u0063\u0074\u0065\u0064 (protected)
 * @onlyStrict
 */




function testcase() {
        "use strict";

        try {
            eval("var \u0070\u0072\u006f\u0074\u0065\u0063\u0074\u0065\u0064 = 123;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
}
runTestCase(testcase);