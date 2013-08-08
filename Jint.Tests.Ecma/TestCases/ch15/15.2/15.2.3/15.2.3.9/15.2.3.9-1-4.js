/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-1-4.js
 * @description Object.freeze throws TypeError if type of first param is string primitive
 */


function testcase() {
        try {
            Object.freeze("abc");
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);
