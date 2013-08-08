/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6-28.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: in (in)
 */


function testcase() {
            try {
                eval("var \u0069\u006e = 123;");  
                return false;
            } catch (e) {
                return e instanceof SyntaxError;  
            }
    }
runTestCase(testcase);
