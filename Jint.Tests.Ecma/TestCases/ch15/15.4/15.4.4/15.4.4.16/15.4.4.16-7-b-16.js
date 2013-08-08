/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-16.js
 * @description Array.prototype.every - decreasing length of array does not delete non-configurable properties
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            if (idx === 2 && val === "unconfigurable") {
                return false;
            } else {
                return true;
            }
        }
        
        var arr = [0, 1, 2];

        Object.defineProperty(arr, "2", {
            get: function () {
                return "unconfigurable";
            },
            configurable: false
        });

        Object.defineProperty(arr, "1", {
            get: function () {
                arr.length = 2;
                return 1;
            },
            configurable: true
        });

        return !arr.every(callbackfn);
    }
runTestCase(testcase);
