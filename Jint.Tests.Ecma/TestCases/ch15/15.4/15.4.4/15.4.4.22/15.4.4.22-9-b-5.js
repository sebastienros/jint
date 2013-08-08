/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-5.js
 * @description Array.prototype.reduceRight - properties added into own object in step 8 can be visited on an Array
 */


function testcase() {

        var testResult = false;

        function callbackfn(preVal, curVal, idx, obj) {
            if (idx === 1 && curVal === 1) {
                testResult = true;
            }
        }

        var arr = [0, , 2];

        Object.defineProperty(arr, "2", {
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

        arr.reduceRight(callbackfn);
        return testResult;
    }
runTestCase(testcase);
