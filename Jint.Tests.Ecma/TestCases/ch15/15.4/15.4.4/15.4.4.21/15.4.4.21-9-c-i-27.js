/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-27.js
 * @description Array.prototype.reduce - This object is the Arguments object which implements its own property get method (number of arguments is greater than number of parameters)
 */


function testcase() {

        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 3) {
                testResult = (curVal === 3);
            }
        }

        var func = function (a, b, c) {
            Array.prototype.reduce.call(arguments, callbackfn, initialValue);
        };

        func(0, 1, 2, 3);
        return testResult;
    }
runTestCase(testcase);
