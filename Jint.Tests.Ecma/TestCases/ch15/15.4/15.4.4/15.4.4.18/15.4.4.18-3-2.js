/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-2.js
 * @description Array.prototype.forEach - value of 'length' is a boolean (value is true)
 */


function testcase() {

        var testResult = false;
        function callbackfn(val, idx, obj) {
            testResult = (val > 10);
        }

        var obj = { 0: 11, 1: 9, length: true };

        Array.prototype.forEach.call(obj, callbackfn);

        return testResult;
    }
runTestCase(testcase);
