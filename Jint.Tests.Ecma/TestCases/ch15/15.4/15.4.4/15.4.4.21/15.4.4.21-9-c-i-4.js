/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-4.js
 * @description Array.prototype.reduce - element to be retrieved is own data property that overrides an inherited data property on an Array
 */


function testcase() {

        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (curVal === 1);
            }
        }

        try {
            Array.prototype[1] = "3";
            [0, 1, 2].reduce(callbackfn, initialValue);
            return testResult;
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
