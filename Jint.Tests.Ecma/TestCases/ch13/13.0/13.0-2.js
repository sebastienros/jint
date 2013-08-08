/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.0/13.0-2.js
 * @description 13.0 - multiple names in one function declaration is not allowed, three function names
 */


function testcase() {
        try {
            eval("function x,y,z(){}");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
