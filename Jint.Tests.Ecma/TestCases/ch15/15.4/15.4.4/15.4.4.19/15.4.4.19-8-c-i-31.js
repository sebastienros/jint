/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-31.js
 * @description Array.prototype.map - unhandled exceptions happened in getter terminate iteration on an Array
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            if (idx > 1) {
                accessed = true;
            }
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

        Object.defineProperty(arr, "2", {
            get: function () {
                accessed = true;
                return 100;
            },
            configurable: true
        });

        try {
            arr.map(callbackfn);
            return false;
        } catch (ex) {
            return (ex instanceof RangeError) && !accessed;
        }
    }
runTestCase(testcase);
