/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-32.js
 * @description Array.prototype.reduceRight - unnhandled exceptions happened in getter terminate iteration on an Array-like object
 */


function testcase() {

        var accessed = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx <= 1) {
                accessed = true;
            }
        }


        var obj = { 0: 0, 2: 2, length: 3 };
        Object.defineProperty(obj, "1", {
            get: function () {
                throw new RangeError("unhandle exception happened in getter");
            },
            configurable: true
        });

        try {
            Array.prototype.reduceRight.call(obj, callbackfn, "initialValue");
            return false;
        } catch (ex) {
            return (ex instanceof RangeError) && !accessed;
        }
    }
runTestCase(testcase);
