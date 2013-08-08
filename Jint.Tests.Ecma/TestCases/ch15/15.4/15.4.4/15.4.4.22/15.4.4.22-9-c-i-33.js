/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-33.js
 * @description Array.prototype.reduceRight - unnhandled exceptions happened in getter terminate iteration on an Array
 */


function testcase() {

        var accessed = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx <= 1) {
                accessed = true;
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
            arr.reduceRight(callbackfn, "initialValue");
            return true;
        } catch (ex) {
            return (ex instanceof RangeError) && !accessed;
        }
    }
runTestCase(testcase);
