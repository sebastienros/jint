/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-14.js
 * @description Array.prototype.map - element to be retrieved is own accessor property that overrides an inherited accessor property on an Array
 */


function testcase() {

        var kValue = "abc";

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === kValue;
            }
            return false;
        }

        var arr = [];

        try {
            Object.defineProperty(Array.prototype, "0", {
                get: function () {
                    return 11;
                },
                configurable: true
            });

            Object.defineProperty(arr, "0", {
                get: function () {
                    return kValue;
                },
                configurable: true
            });

            var testResult = arr.map(callbackfn);

            return testResult[0] === true;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
