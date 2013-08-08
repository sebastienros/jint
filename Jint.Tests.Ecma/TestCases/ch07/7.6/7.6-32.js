/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6-32.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: enum (enum)
 */


function testcase() {
            try {
                eval("var \u0065\u006e\u0075\u006d = 123;");  
                return false;
            } catch (e) {
                return e instanceof SyntaxError;  
            }
    }
runTestCase(testcase);
