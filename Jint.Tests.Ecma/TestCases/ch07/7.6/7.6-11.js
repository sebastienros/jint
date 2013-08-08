/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6-11.js
 * @description 7.6 - SyntaxError expected: reserved words used as Identifier Names in UTF8: var (var)
 */


function testcase() {
            try {
                eval("var va\u0072 = 123;");  
                return false;
            } catch (e) {
                return e instanceof SyntaxError;  
            }
    }
runTestCase(testcase);
