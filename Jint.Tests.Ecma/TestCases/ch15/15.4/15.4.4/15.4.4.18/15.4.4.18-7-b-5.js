/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-5.js
 * @description Array.prototype.forEach - properties added into own object after current position are visited on an Array
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 1 && val === 1) {
                testResult = true;
            }
        }

        var arr = [0, , 2];

        Object.defineProperty(arr, "0", {
            get: function () {
                Object.defineProperty(arr, "1", {
                    get: function () {
                        return 1;
                    },
                    configurable: true
                });
                return 0;
            },
            configurable: true
        });

        arr.forEach(callbackfn);
        return testResult;
    }
runTestCase(testcase);
