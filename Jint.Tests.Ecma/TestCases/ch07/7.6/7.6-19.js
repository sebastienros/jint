/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6-19.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: switch (switch)
 */


function testcase() {
            try {
                eval("var switc\u0068 = 123;");  
                return false;
            } catch (e) {
                return e instanceof SyntaxError;  
            }
    }
runTestCase(testcase);
