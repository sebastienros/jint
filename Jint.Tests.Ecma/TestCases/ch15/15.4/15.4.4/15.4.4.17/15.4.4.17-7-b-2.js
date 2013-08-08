/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-2.js
 * @description Array.prototype.some - added properties in step 2 are visible here
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            if (idx === 2 && val === "length") {
                return true;
            } else {
                return false;
            }
        }
        
        var arr = { };

        Object.defineProperty(arr, "length", {
            get: function () {
                arr[2] = "length";
                return 3;
            },
            configurable: true
        });

        return Array.prototype.some.call(arr, callbackfn);
    }
runTestCase(testcase);
