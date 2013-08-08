/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-4.js
 * @description Array.prototype.forEach - element to be retrieved is own data property that overrides an inherited data property on an Array
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                testResult = (val === 12);
            }
        }

        try {
            Array.prototype[0] = 11;

            [12].forEach(callbackfn);

            return testResult;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
