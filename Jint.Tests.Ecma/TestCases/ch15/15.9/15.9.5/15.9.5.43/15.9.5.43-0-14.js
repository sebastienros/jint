/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-14.js
 * @description Date.prototype.toISOString - when value of year is -Infinity Date.prototype.toISOString throw the RangeError
 */


function testcase() {
        var date = new Date(-Infinity, 1, 70, 0, 0, 0);

        try {
            date.toISOString();
        } catch (ex) {
            return ex instanceof RangeError;
        }
    }
runTestCase(testcase);
