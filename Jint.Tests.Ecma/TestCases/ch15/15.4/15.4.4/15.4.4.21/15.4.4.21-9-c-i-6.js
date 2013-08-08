/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-6.js
 * @description Array.prototype.reduce - element to be retrieved is own data property that overrides an inherited accessor property on an Array
 */


function testcase() {

        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (curVal === 1);
            }
        }

        try {
            Object.defineProperty(Array.prototype, "1", {
                get: function () {
                    return "9";
                },
                configurable: true
            });
            [0, 1, 2].reduce(callbackfn, initialValue);
            return testResult;

        } finally {
            delete Array.prototype[1];
        }

    }
runTestCase(testcase);
