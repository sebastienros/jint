/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-10.js
 * @description Array.prototype.reduce - when element to be retrieved is own accessor property on an Array
 */


function testcase() {
        var testResult = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === 0);
            }
        }

        var arr = [, 1, 2];

        Object.defineProperty(arr, "0", {
            get: function () {
                return 0;
            },
            configurable: true
        });

        arr.reduce(callbackfn);
        return testResult;
    }
runTestCase(testcase);
