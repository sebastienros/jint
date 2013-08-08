/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-1.js
 * @description Array.prototype.forEach - element to be retrieved is own data property on an Array-like object
 */


function testcase() {

        var kValue = { };
        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 5) {
                testResult = (val === kValue);
            }
        }

        var obj = { 5: kValue, length: 100 };

        Array.prototype.forEach.call(obj, callbackfn);

        return testResult;
    }
runTestCase(testcase);
