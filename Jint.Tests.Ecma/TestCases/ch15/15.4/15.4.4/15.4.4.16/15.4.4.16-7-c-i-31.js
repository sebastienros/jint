/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-31.js
 * @description Array.prototype.every - unhandled exceptions happened in getter terminate iteration on an Array
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            if (idx > 1) {
                accessed = true;
            }
            return true;
        }

        var arr = [];
        arr[5] = 10;
        arr[10] = 100;

        Object.defineProperty(arr, "1", {
            get: function () {
                throw new RangeError("unhandle exception happened in getter");
            },
            configurable: true
        });

        try {
            arr.every(callbackfn);
            return false;
        } catch (ex) {
            return (ex instanceof RangeError) && !accessed;
        }
    }
runTestCase(testcase);
