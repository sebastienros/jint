/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-15.js
 * @description Array.prototype.forEach - 'length' is a string containing an exponential number
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            testResult = (val > 10);
        }

        var obj = { 1: 11, 2: 9, length: "2E0" };

        Array.prototype.forEach.call(obj, callbackfn);
        return testResult;
    }
runTestCase(testcase);
