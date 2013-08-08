/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-18.js
 * @description Array.prototype.reduce - properties added into own object are visited on an Array
 */


function testcase() {

        var testResult = false;

        function callbackfn(accum, val, idx, obj) {
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

        arr.reduce(callbackfn, "initialValue");
        return testResult;
    }
runTestCase(testcase);
