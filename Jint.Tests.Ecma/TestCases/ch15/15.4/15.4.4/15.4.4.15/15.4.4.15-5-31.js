/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-31.js
 * @description Array.prototype.lastIndexOf - 'fromIndex' is a positive non-integer, verify truncation occurs in the proper direction
 */


function testcase() {
        var targetObj = {};
        return [0, targetObj, true].lastIndexOf(targetObj, 1.5) === 1 &&
            [0, true, targetObj].lastIndexOf(targetObj, 1.5) === -1;
    }
runTestCase(testcase);
