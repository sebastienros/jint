/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-9.js
 * @description Array.prototype.map - deleting own property causes index property not to be visited on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                return false;
            } else {
                return true;
            }
        }
        var arr = [1, 2];

        Object.defineProperty(arr, "1", {
            get: function () {
                return "6.99";
            },
            configurable: true
        });

        Object.defineProperty(arr, "0", {
            get: function () {
                delete arr[1];
                return 0;
            },
            configurable: true
        });

        var testResult = arr.map(callbackfn);
        return testResult[0] === true && typeof testResult[1] === "undefined";
    }
runTestCase(testcase);
