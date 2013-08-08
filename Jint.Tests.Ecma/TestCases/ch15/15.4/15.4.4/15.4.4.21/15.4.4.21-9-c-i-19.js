/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-19.js
 * @description Array.prototype.reduce - element to be retrieved is own accessor property without a get function that overrides an inherited accessor property on an Array-like object
 */


function testcase() {

        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (curVal === undefined);
            }
        }

        try {
            Object.defineProperty(Object.prototype, "1", {
                get: function () {
                    return 1;
                },
                configurable: true
            });

            var obj = { 0: 0, 2: 2, length: 3 };

            Object.defineProperty(obj, "1", {
                set: function () { },
                configurable: true
            });

            Array.prototype.reduce.call(obj, callbackfn, initialValue);
            return testResult;
        } finally {
            delete Object.prototype[1];
        }
    }
runTestCase(testcase);
