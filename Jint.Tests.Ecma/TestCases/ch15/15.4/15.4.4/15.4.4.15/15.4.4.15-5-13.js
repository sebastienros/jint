/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-13.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is a number (value is -Infinity)
 */


function testcase() {

        return [true].lastIndexOf(true, -Infinity) === -1;
    }
runTestCase(testcase);
