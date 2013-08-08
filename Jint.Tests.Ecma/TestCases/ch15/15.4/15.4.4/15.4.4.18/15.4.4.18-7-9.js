/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-9.js
 * @description Array.prototype.forEach - modifications to length don't change number of iterations
 */


function testcase() {

        var called = 0;
        function callbackfn(val, idx, obj) {
            called++;
        }

        var obj = { 1: 12, 2: 9, length: 2 };

        Object.defineProperty(obj, "0", {
            get: function () {
                obj.length = 3;
                return 11;
            },
            configurable: true
        });

        Array.prototype.forEach.call(obj, callbackfn);
        return 2 === called;
    }
runTestCase(testcase);
