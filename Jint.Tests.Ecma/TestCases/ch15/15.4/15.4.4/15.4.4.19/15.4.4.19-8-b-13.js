/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-13.js
 * @description Array.prototype.map - deleting own property with prototype property causes prototype index property to be visited on an Array
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            if (idx === 1 && val === 3) {
                return false;
            } else {
                return true;
            }
        }
        var arr = [0, 1, 2];

        Object.defineProperty(arr, "0", {
            get: function () {
                delete arr[1];
                return 0;
            },
            configurable: true
        });

        try {
            Array.prototype[1] = 3;
            var testResult = arr.map(callbackfn);
            return testResult[1] === false;
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
