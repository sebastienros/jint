/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-19.js
 * @description Array.prototype.forEach - element to be retrieved is own accessor property without a get function that overrides an inherited accessor property on an Array-like object
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                testResult = (typeof val === "undefined");
            }
        }

        var obj = { length: 2 };

        Object.defineProperty(obj, "1", {
            set: function () { },
            configurable: true
        });

        try {
            Object.defineProperty(Object.prototype, "1", {
                get: function () {
                    return 10;
                },
                configurable: true
            });

            Array.prototype.forEach.call(obj, callbackfn);

            return testResult;
        } finally {
            delete Object.prototype[1];
        }
    }
runTestCase(testcase);
