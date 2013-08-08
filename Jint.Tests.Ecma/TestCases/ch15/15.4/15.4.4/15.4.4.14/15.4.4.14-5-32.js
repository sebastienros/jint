/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-32.js
 * @description Array.prototype.indexOf - 'fromIndex' is a negative non-integer, verify truncation occurs in the proper direction
 */


function testcase() {
        var targetObj = {};
        return [0, targetObj, 2].indexOf(targetObj, -1.5) === -1 &&
            [0, 1, targetObj].indexOf(targetObj, -1.5) === 2;
    }
runTestCase(testcase);
