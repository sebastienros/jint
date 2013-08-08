/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-6.js
 * @description Date.prototype.toISOString - TypeError is thrown when this is any other objects instead of Date object
 */


function testcase() {

        try {
            Date.prototype.toISOString.call([]);
            return false;
        } catch (ex) {
            return ex instanceof TypeError;
        }
    }
runTestCase(testcase);
