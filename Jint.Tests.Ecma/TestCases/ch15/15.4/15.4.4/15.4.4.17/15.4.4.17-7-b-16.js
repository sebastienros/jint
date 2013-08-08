/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-16.js
 * @description Array.prototype.some - decreasing length of array does not delete non-configurable properties
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            if (idx === 2 && val === "unconfigurable") {
                return true;
            } else {
                return false;
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

        return arr.some(callbackfn);
    }
runTestCase(testcase);
