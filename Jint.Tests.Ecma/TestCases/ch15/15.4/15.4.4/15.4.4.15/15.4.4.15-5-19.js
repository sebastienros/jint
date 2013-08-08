/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-19.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is a string containing a hex number
 */


function testcase() {
        var targetObj = {};
        return [0, true, targetObj, 3, false].lastIndexOf(targetObj, "0x0002") === 2 &&
            [0, true, 3, targetObj, false].lastIndexOf(targetObj, "0x0002") === -1;
    }
runTestCase(testcase);
