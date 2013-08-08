/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-2.js
 * @description Array.prototype.reduceRight - added properties in step 2 are visible here
 */


function testcase() {

        var testResult = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 2 && curVal === "2") {
                testResult = true;
            }
        }

        var obj = {};

        Object.defineProperty(obj, "length", {
            get: function () {
                obj[2] = "2";
                return 3;
            },
            configurable: true
        });

        Array.prototype.reduceRight.call(obj, callbackfn, "initialValue");

        return testResult;
    }
runTestCase(testcase);
