/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-18.js
 * @description Array.prototype.reduce - element to be retrieved is own accessor property without a get function on an Array
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === undefined);
            }
        }

        var arr = [, 1, 2];

        Object.defineProperty(arr, "0", {
            set: function () { },
            configurable: true
        });

        arr.reduce(callbackfn);
        return testResult;
    }
runTestCase(testcase);
