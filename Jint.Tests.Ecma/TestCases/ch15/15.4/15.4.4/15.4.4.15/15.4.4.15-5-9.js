/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-9.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is a number (value is -0)
 */


function testcase() {

        return [0, true].lastIndexOf(true, -0) === -1 &&
            [true, 0].lastIndexOf(true, -0) === 0;
    }
runTestCase(testcase);
