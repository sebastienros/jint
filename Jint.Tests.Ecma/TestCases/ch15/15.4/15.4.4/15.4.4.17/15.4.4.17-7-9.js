/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-9.js
 * @description Array.prototype.some - modifications to length don't change number of iterations
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return val > 10;
        }

        var obj = { 0: 9, 2: 12, length: 3 };

        Object.defineProperty(obj, "1", {
            get: function () {
                obj.length = 2;
                return 8;
            },
            configurable: true
        });

        return Array.prototype.some.call(obj, callbackfn) && called === 3;
    }
runTestCase(testcase);
