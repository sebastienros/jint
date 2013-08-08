/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-16.js
 * @description Array.prototype.reduceRight - non-indexed properties are not called on an Array-like object
 */


function testcase() {

        var testResult = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            if (prevVal === 8 || curVal === 8) {
                testResult = true;
            }
        }

        var obj = { 0: 11, 10: 12, non_index_property: 8, length: 20 };
        Array.prototype.reduceRight.call(obj, callbackfn, "initialValue");
        return !testResult;
    }
runTestCase(testcase);
