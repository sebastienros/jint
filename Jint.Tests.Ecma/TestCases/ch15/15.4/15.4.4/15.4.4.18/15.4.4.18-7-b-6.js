/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-6.js
 * @description Array.prototype.forEach - properties can be added to prototype after current position are visited on an Array-like object
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 1 && val === 6.99) {
                testResult = true;
            }
        }

        var obj = { length: 2 };

        Object.defineProperty(obj, "0", {
            get: function () {
                Object.defineProperty(Object.prototype, "1", {
                    get: function () {
                        return 6.99;
                    },
                    configurable: true
                });
                return 0;
            },
            configurable: true
        });

        try {
            Array.prototype.forEach.call(obj, callbackfn);
            return testResult;
        } finally {
            delete Object.prototype[1];
        }
    }
runTestCase(testcase);
