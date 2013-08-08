/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-16.js
 * @description Array.prototype.reduceRight - decreasing length of array in step 8 does not delete non-configurable properties
 */


function testcase() {

        var testResult = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 2 && curVal === "unconfigurable") {
                testResult = true;
            }
        }

        var arr = [0, 1, 2, 3];

        Object.defineProperty(arr, "2", {
            get: function () {
                return "unconfigurable";
            },
            configurable: false
        });

        Object.defineProperty(arr, "3", {
            get: function () {
                arr.length = 2;
                return 1;
            },
            configurable: true
        });

        arr.reduceRight(callbackfn);

        return testResult;
    }
runTestCase(testcase);
