/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-1-2.js
 * @description Object.getOwnPropertyDescriptor - TypeError is thrown when first param is null
 */


function testcase() {
        try {
            Object.getOwnPropertyDescriptor(null, "foo");
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
