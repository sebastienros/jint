/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-10.js
 * @description Array.prototype.every - element to be retrieved is own accessor property on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 2) {
                return val !== 12;
            } else {
                return true;
            }
        }

        var arr = [];

        Object.defineProperty(arr, "2", {
            get: function () {
                return 12;
            },
            configurable: true
        });

        return !arr.every(callbackfn);
    }
runTestCase(testcase);
