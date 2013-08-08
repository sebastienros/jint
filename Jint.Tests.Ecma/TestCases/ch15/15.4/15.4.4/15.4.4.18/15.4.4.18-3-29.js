/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-29.js
 * @description Array.prototype.forEach - value of 'length' is boundary value (2^32 + 1)
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            testResult = (val > 10);
        }

        var obj = {
            0: 11,
            1: 9,
            length: 4294967297
        };

        Array.prototype.forEach.call(obj, callbackfn);

        return testResult;
    }
runTestCase(testcase);
