/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-9.js
 * @description Array.prototype.reduce - modifications to length don't change number of iterations in step 9
 */


function testcase() {
        var called = 0;
        function callbackfn(accum, val, idx, obj) {
            called++;
            return accum + val;
        }

        var arr = [0, 1, 2, 3];
        Object.defineProperty(arr, "0", {
            get: function () {
                arr.length = 2;
                return 0;
            },
            configurable: true
        });

        var newAccum = arr.reduce(callbackfn, "initialValue");

        return newAccum === "initialValue01" && called === 2;
    }
runTestCase(testcase);
