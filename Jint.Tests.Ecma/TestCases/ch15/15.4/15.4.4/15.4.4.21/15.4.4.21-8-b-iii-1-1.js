/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-iii-1-1.js
 * @description Array.prototype.reduce - element to be retrieved is own data property on an Array-like object
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === 0);
            }
        }

        var obj = { 0: 0, 1: 1, 2: 2, length: 2 };
        Array.prototype.reduce.call(obj, callbackfn);
        return testResult;
    }
runTestCase(testcase);
