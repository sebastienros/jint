/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6-12.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: try (try)
 */


function testcase() {
            try {
                eval("var \u0074\u0072\u0079 = 123;");  
                return false;
            } catch (e) {
                return e instanceof SyntaxError;  
            }
    }
runTestCase(testcase);
