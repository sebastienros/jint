/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-7.js
 * @description Date.prototype.toISOString - TypeError is thrown when this is any primitive values
 */


function testcase() {

        try {
            Date.prototype.toISOString.call(15);
            return false;
        } catch (ex) {
            return ex instanceof TypeError;
        }
    }
runTestCase(testcase);
