/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-32.js
 * @description Array.prototype.lastIndexOf - 'fromIndex' is a negative non-integer, verify truncation occurs in the proper direction
 */


function testcase() {
        var targetObj = {};
        return [0, targetObj, true].lastIndexOf(targetObj, -2.5) === 1 &&
            [0, true, targetObj].lastIndexOf(targetObj, -2.5) === -1;

    }
runTestCase(testcase);
