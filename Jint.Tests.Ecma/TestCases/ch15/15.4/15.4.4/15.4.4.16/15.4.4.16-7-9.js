/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-9.js
 * @description Array.prototype.every - modifications to length don't change number of iterations
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return val > 10;
        }

        var obj = { 1: 12, 2: 9, length: 2 };

        Object.defineProperty(obj, "0", {
            get: function () {
                obj.length = 3;
                return 11;
            },
            configurable: true
        });

        return Array.prototype.every.call(obj, callbackfn) && 2 === called;
    }
runTestCase(testcase);
