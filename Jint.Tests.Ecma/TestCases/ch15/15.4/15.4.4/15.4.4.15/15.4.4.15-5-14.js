/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-14.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is a number (value is NaN)
 */


function testcase() {

        return [0, true].lastIndexOf(true, NaN) === -1 && // from Index will be convert to +0
            [true, 0].lastIndexOf(true, NaN) === 0 &&
            [0, true].lastIndexOf(true, -NaN) === -1 &&
            [true, 0].lastIndexOf(true, -NaN) === 0;
    }
runTestCase(testcase);
