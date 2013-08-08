/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-29.js
 * @description Array.prototype.forEach - element changed by getter on previous iterations is observed on an Array-like object
 */


function testcase() {

        var preIterVisible = false;
        var obj = { length: 2 };
        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                testResult = (val === 9);
            }
        }

        Object.defineProperty(obj, "0", {
            get: function () {
                preIterVisible = true;
                return 11;
            },
            configurable: true
        });

        Object.defineProperty(obj, "1", {
            get: function () {
                if (preIterVisible) {
                    return 9;
                } else {
                    return 13;
                }
            },
            configurable: true
        });

        Array.prototype.forEach.call(obj, callbackfn);

        return testResult;
    }
runTestCase(testcase);
