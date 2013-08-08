/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-20.js
 * @description Array.prototype.reduce - element to be retrieved is own accessor property without a get function that overrides an inherited accessor property on an Array
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === undefined);
            }
        }

        try {
            Array.prototype[0] = 0;
            var arr = [, 1, 2];
            Object.defineProperty(arr, "0", {
                set: function () { },
                configurable: true
            });

            arr.reduce(callbackfn);
            return testResult;

        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
