/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-31.js
 * @description Array.prototype.indexOf - 'fromIndex' is a positive non-integer, verify truncation occurs in the proper direction
 */


function testcase() {
        var targetObj = {};
        return [0, targetObj, 2].indexOf(targetObj, 2.5) === -1 &&
            [0, 1, targetObj].indexOf(targetObj, 2.5) === 2;
    }
runTestCase(testcase);
