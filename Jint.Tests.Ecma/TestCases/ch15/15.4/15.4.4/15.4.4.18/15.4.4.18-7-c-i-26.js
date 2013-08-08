/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-26.js
 * @description Array.prototype.forEach - This object is the Arguments object which implements its own property get method (number of arguments equals number of parameters)
 */


function testcase() {

        var called = 0;
        var testResult = false;

        function callbackfn(val, idx, obj) {
            called++;
            if (called !== 1 && !testResult) {
                return;
            }
            if (idx === 0) {
                testResult = (val === 11);
            } else if (idx === 1) {
                testResult = (val === 9);
            } else {
                testResult = false;
            }
        }

        var func = function (a, b) {
            Array.prototype.forEach.call(arguments, callbackfn);
        };

        func(11, 9);

        return testResult;
    }
runTestCase(testcase);
