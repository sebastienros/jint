/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-30.js
 * @description Array.prototype.some - unhandled exceptions happened in getter terminate iteration on an Array-like object
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            if (idx > 1) {
                accessed = true;
            }
            return true;
        }

        var obj = { length: 20 };
        Object.defineProperty(obj, "1", {
            get: function () {
                throw new RangeError("unhandle exception happened in getter");
            },
            configurable: true
        });

        try {
            Array.prototype.some.call(obj, callbackfn);
            return false;
        } catch (ex) {
            return ex instanceof RangeError && !accessed;
        }
    }
runTestCase(testcase);
