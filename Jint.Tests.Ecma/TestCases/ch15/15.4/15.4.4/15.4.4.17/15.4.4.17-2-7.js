/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-2-7.js
 * @description Array.prototype.some - 'length' is an own accessor property on an Array-like object
 */


function testcase() {
        function callbackfn1(val, idx, obj) {
            return val > 10;
        }

        function callbackfn2(val, idx, obj) {
            return val > 11;
        }

        var obj = { };

        Object.defineProperty(obj, "length", {
            get: function () {
                return 2;
            },
            configurable: true
        });

        obj[0] = 9;
        obj[1] = 11;
        obj[2] = 12;

        return Array.prototype.some.call(obj, callbackfn1) &&
            !Array.prototype.some.call(obj, callbackfn2);
    }
runTestCase(testcase);
