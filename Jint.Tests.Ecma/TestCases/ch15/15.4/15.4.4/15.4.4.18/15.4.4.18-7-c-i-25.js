/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-25.js
 * @description Array.prototype.forEach - This object is the Arguments object which implements its own property get method (number of arguments is less than number of parameters)
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                testResult = (val === 11);
            }
        }

        var func = function (a, b) {
            return Array.prototype.forEach.call(arguments, callbackfn);
        };

        func(11);

        return testResult;
    }
runTestCase(testcase);
