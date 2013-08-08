/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-13.js
 * @description Array.prototype.forEach - deleting own property with prototype property causes prototype index property to be visited on an Array
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 1 && val === 1) {
                testResult = true;
            }
        }
        var arr = [0, 111];

        Object.defineProperty(arr, "0", {
            get: function () {
                delete arr[1];
                return 0;
            },
            configurable: true
        });

        try {
            Array.prototype[1] = 1;
            arr.forEach(callbackfn);
            return testResult;
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
