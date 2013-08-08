/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-4.js
 * @description Object.isFrozen - TypeError is thrown when the first param 'O' is a string
 */


function testcase() {
        try {
            Object.isFrozen("abc");
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
