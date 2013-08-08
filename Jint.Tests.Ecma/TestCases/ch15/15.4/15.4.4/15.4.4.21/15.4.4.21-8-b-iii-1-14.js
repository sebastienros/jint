/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-14.js
 * @description Array.prototype.reduce - element to be retrieved is own accessor property that overrides an inherited accessor property on an Array
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === "9");
            }
        }

        try {
            Object.defineProperty(Array.prototype, "0", {
                get: function () {
                    return 0;
                },
                configurable: true
            });

            var arr = [, 1, 2];
            Object.defineProperty(arr, "0", {
                get: function () {
                    return "9";
                },
                configurable: true
            });

            arr.reduce(callbackfn);
            return testResult;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
