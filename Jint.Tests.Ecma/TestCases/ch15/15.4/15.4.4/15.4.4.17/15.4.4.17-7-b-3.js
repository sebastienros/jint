/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-3.js
 * @description Array.prototype.some - deleted properties in step 2 are visible here
 */


function testcase() {
        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return idx === 2;
        }
        var arr = { 2: 6.99, 8: 19};

        Object.defineProperty(arr, "length", {
            get: function () {
                delete arr[2];
                return 10;
            },
            configurable: true
        });

        return !Array.prototype.some.call(arr, callbackfn) && accessed;
    }
runTestCase(testcase);
