/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-1.js
 * @description Array.prototype.reduceRight - element to be retrieved is own data property on an Array-like object
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 0) {
                testResult = (prevVal === 1);
            }
        }

        var obj = { 0: 0, 1: 1, length: 2 };

        Array.prototype.reduceRight.call(obj, callbackfn);
        return testResult;
    }
runTestCase(testcase);
