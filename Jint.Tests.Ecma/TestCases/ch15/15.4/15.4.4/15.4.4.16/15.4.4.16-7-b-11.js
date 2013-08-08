/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-11.js
 * @description Array.prototype.every - deleting property of prototype causes prototype index property not to be visited on an Array
 */


function testcase() {
        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return idx !== 1;
        }
        var arr = [0, , 2];

        Object.defineProperty(arr, "0", {
            get: function () {
                delete Array.prototype[1];
                return 0;
            },
            configurable: true
        });

        try {
            Array.prototype[1] = 1;
            return arr.every(callbackfn) && accessed;
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
