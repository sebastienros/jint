/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-1.js
 * @description Array.prototype.reduceRight - element to be retrieved is own data property on an Array-like object
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 0) {
                testResult = (curVal === 0);
            }
        }

        var obj = { 0: 0, 1: 1, 2: 2, length: 2 };
        Array.prototype.reduceRight.call(obj, callbackfn, "initialValue");
        return testResult;
    }
runTestCase(testcase);
