/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-18.js
 * @description Array.prototype.forEach - element to be retrieved is own accessor property without a get function on an Array
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                testResult = (typeof val === "undefined");
            }
        }

        var arr = [];

        Object.defineProperty(arr, "0", {
            set: function () { },
            configurable: true
        });

        arr.forEach(callbackfn);

        return testResult;
    }
runTestCase(testcase);
