/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-20.js
 * @description Array.prototype.indexOf - value of 'fromIndex' which is a string containing a number with leading zeros
 */


function testcase() {
        var targetObj = {};
        return [0, 1, targetObj, 3, 4].indexOf(targetObj, "0003.10") === -1 &&
            [0, 1, 2, targetObj, 4].indexOf(targetObj, "0003.10") === 3;
    }
runTestCase(testcase);
