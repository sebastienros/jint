/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-2.js
 * @description Array.prototype.every - added properties in step 2 are visible here
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            if (idx === 2 && val === "length") {
                return false;
            } else {
                return true;
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

        return !Array.prototype.every.call(arr, callbackfn);
    }
runTestCase(testcase);
