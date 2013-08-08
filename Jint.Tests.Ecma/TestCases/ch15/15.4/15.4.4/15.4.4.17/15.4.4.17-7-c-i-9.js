/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-9.js
 * @description Array.prototype.some - element to be retrieved is own accessor property on an Array-like object
 */


function testcase() {

        var kValue = "abc";

        function callbackfn(val, idx, obj) {
            if (idx === 10) {
                return val === kValue;
            }
            return false;
        }

        var obj = { length: 20 };

        Object.defineProperty(obj, "10", {
            get: function () {
                return kValue;
            },
            configurable: true
        });

        return Array.prototype.some.call(obj, callbackfn);
    }
runTestCase(testcase);
