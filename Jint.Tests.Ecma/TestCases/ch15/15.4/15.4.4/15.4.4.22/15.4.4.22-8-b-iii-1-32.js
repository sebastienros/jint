/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-32.js
 * @description Array.prototype.reduceRight - Exception in getter terminate iteration on an Array-like object
 */


function testcase() {

        var accessed = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx <= 1) {
                accessed = true;
            }
        }

        var obj = { 0: 0, 1: 1, length: 3 };
        Object.defineProperty(obj, "2", {
            get: function () {
                throw new RangeError("unhandle exception happened in getter");
            },
            configurable: true
        });

        try {
            Array.prototype.reduceRight.call(obj, callbackfn);
            return false;
        } catch (ex) {
            return (ex instanceof RangeError) && !accessed;
        }
    }
runTestCase(testcase);
