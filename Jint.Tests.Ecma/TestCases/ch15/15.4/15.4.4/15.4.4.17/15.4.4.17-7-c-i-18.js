/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-18.js
 * @description Array.prototype.some - element to be retrieved is own accessor property without a get function on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return typeof val === "undefined";
            }
            return false;
        }

        var arr = [];

        Object.defineProperty(arr, "0", {
            set: function () { },
            configurable: true
        });

        return arr.some(callbackfn);
    }
runTestCase(testcase);
