/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-1-4.js
 * @description Object.getPrototypeOf throws TypeError if 'O' is a string
 */


function testcase() {
        try {
            Object.getPrototypeOf("abc");
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
