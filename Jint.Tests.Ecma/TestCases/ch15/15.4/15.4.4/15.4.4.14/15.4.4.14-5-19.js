/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-19.js
 * @description Array.prototype.indexOf - value of 'fromIndex' is a string containing a hex number
 */


function testcase() {
        var targetObj = {};
        return [0, 1, targetObj, 3, 4].indexOf(targetObj, "0x0003") === -1 &&
            [0, 1, 2, targetObj, 4].indexOf(targetObj, "0x0003") === 3;
    }
runTestCase(testcase);
