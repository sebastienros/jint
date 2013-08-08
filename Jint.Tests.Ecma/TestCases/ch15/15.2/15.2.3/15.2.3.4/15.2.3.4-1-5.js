/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-1-5.js
 * @description Object.getOwnPropertyNames throws TypeError if 'O' is a string
 */


function testcase() {
        try {
            Object.getOwnPropertyNames("abc");
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);
