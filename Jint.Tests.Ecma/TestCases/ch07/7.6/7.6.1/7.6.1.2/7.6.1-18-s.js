/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1.2/7.6.1-18-s.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: l\u0065t (let)
 * @onlyStrict 
 */


function testcase() {        
        "use strict";

        try {
            eval("var l\u0065t = 123;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
}
runTestCase(testcase);