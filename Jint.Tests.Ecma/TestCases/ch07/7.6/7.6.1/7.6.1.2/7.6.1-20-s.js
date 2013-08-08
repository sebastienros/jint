/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1.2/7.6.1-20-s.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: \u0070\u0075\u0062\u006c\u0069\u0063 (public)
 * @onlyStrict
 */

function testcase() {
        "use strict";

        try {
            eval("var \u0070\u0075\u0062\u006c\u0069\u0063 = 123;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
}
runTestCase(testcase);