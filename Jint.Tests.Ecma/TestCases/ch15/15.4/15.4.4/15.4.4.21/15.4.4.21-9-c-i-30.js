/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-30.js
 * @description Array.prototype.reduce - element changed by getter on previous iterations is observed on an Array
 */


function testcase() {

        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (curVal === 1);
            }
        }

        var arr = [, , 2];
        var preIterVisible = false;

        Object.defineProperty(arr, "0", {
            get: function () {
                preIterVisible = true;
                return 0;
            },
            configurable: true
        });

        Object.defineProperty(arr, "1", {
            get: function () {
                if (preIterVisible) {
                    return 1;
                } else {
                    return 100;
                }
            },
            configurable: true
        });

        arr.reduce(callbackfn, initialValue);
        return testResult;
    }
runTestCase(testcase);
