/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6-20.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: while (while)
 */


function testcase() {
            try {
                eval("var \u0077\u0068\u0069\u006c\u0065 = 123;");  
                return false;
            } catch (e) {
                return e instanceof SyntaxError;  
            }
    }
runTestCase(testcase);
