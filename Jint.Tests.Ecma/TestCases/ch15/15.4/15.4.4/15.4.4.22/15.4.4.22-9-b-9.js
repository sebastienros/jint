/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-9.js
 * @description Array.prototype.reduceRight - deleting own property in step 8 causes deleted index property not to be visited on an Array
 */


function testcase() {

        var accessed = false;
        var testResult = true;

        function callbackfn(preVal, curVal, idx, obj) {
            accessed = true;
            if (idx === 1) {
                testResult = false;
            }
        }

        var arr = [0];

        Object.defineProperty(arr, "1", {
            get: function () {
                return "6.99";
            },
            configurable: true
        });

        Object.defineProperty(arr, "2", {
            get: function () {
                delete arr[1];
                return 0;
            },
            configurable: true
        });

        arr.reduceRight(callbackfn);
        return testResult && accessed;
    }
runTestCase(testcase);
