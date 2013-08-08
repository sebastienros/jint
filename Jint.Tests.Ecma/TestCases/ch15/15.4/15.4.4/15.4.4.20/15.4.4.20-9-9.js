/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-9.js
 * @description Array.prototype.filter - modifications to length don't change number of iterations
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return true;
        }

        var obj = { 1: 12, 2: 9, length: 2 };

        Object.defineProperty(obj, "0", {
            get: function () {
                obj.length = 3;
                return 11;
            },
            configurable: true
        });

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 2 && 2 === called;
    }
runTestCase(testcase);
