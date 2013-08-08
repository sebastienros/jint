/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-18.js
 * @description Array.prototype.every - element to be retrieved is own accessor property without a get function on an Array
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return typeof val === "undefined";
        }

        var arr = [];

        Object.defineProperty(arr, "0", {
            set: function () { },
            configurable: true
        });

        return arr.every(callbackfn) && accessed;
    }
runTestCase(testcase);
