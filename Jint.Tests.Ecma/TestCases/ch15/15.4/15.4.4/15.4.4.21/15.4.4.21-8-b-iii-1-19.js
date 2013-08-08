/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-19.js
 * @description Array.prototype.reduce - element to be retrieved is own accessor property without a get function that overrides an inherited accessor property on an Array-like object
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === undefined);
            }
        }

        try {
            Object.prototype[0] = 0;

            var obj = { 1: 1, 2: 2, length: 3 };

            Object.defineProperty(obj, "0", {
                set: function () { },
                configurable: true
            });

            Array.prototype.reduce.call(obj, callbackfn);
            return testResult;
        } finally {
            delete Object.prototype[0];
        }
    }
runTestCase(testcase);
