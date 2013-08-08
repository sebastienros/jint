/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6-31.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: extends (extends)
 */


function testcase() {
            try {
                eval("var extend\u0073 = 123;");  
                return false;
            } catch (e) {
                return e instanceof SyntaxError;  
            }
    }
runTestCase(testcase);
