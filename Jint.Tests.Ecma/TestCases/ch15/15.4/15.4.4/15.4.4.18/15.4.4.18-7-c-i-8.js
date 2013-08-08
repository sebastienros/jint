/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-8.js
 * @description Array.prototype.forEach - element to be retrieved is inherited data property on an Array
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                testResult = (val === 13);
            }
        }

        try {
            Array.prototype[1] = 13;

            [, , , ].forEach(callbackfn);

            return testResult;
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
