/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-10.js
 * @description Array.prototype.indexOf - value of 'fromIndex' is a number (value is positive number)
 */


function testcase() {
        var targetObj = {};
        return [0, targetObj, 2].indexOf(targetObj, 2) === -1 &&
            [0, 1, targetObj].indexOf(targetObj, 2) === 2;
    }
runTestCase(testcase);
