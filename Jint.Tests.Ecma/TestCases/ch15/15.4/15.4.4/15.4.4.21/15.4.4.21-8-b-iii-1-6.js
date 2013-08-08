/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-6.js
 * @description Array.prototype.reduce - element to be retrieved is own data property that overrides an inherited accessor property on an Array
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === 0);
            }
        }

        try {
            Object.defineProperty(Array.prototype, "0", {
                get: function () {
                    return "5";
                },
                configurable: true
            });

            [0, 1, 2].reduce(callbackfn);
            return testResult;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
