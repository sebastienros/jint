/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-11.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is a number (value is negative number)
 */


function testcase() {
        var targetObj = {};
        return [0, targetObj, true].lastIndexOf(targetObj, -2.5) === 1 &&
            [0, true, targetObj].lastIndexOf(targetObj, -2.5) === -1;
    }
runTestCase(testcase);
