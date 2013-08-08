/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-17.js
 * @description Array.prototype.reduceRight - element to be retrieved is own accessor property without a get function on an Array-like object
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (typeof curVal === "undefined");
            }
        }

        var obj = { 0: 0, 2: 2, length: 3 };

        Object.defineProperty(obj, "1", {
            set: function () { },
            configurable: true
        });

        Array.prototype.reduceRight.call(obj, callbackfn, "initialValue");
        return testResult;
    }
runTestCase(testcase);
