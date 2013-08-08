/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-1-4.js
 * @description Object.getOwnPropertyDescriptor - TypeError is thrown when first param is a number
 */


function testcase() {
        try {
            Object.getOwnPropertyDescriptor(-2, "foo");
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
