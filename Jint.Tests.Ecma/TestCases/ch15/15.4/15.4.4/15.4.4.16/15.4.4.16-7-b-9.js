/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-9.js
 * @description Array.prototype.every - deleting own property causes index property not to be visited on an Array
 */


function testcase() {
        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return idx !== 1;
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

        return arr.every(callbackfn) && accessed;
    }
runTestCase(testcase);
