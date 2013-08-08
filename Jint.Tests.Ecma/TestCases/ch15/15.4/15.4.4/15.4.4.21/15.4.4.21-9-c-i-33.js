/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-33.js
 * @description Array.prototype.reduce - unnhandled exceptions happened in getter terminate iteration on an Array
 */


function testcase() {

        var accessed = false;
        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx >= 1) {
                accessed = true;
                testResult = (curVal >= 1);
            }
        }

        var arr = [0, , 2];

        Object.defineProperty(arr, "1", {
            get: function () {
                throw new RangeError("unhandle exception happened in getter");
            },
            configurable: true
        });

        try {
            arr.reduce(callbackfn, initialValue);
            return false;
        } catch (ex) {
            return (ex instanceof RangeError) && !accessed && !testResult;
        }
    }
runTestCase(testcase);
