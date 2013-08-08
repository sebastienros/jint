/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-7.js
 * @description Array.prototype.forEach - unhandled exceptions happened in callbackfn terminate iteration
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            if (idx > 0) {
                accessed = true;
            }
            if (idx === 0) {
                throw new Error("Exception occurred in callbackfn");
            }
        }

        var obj = { 0: 11, 4: 10, 10: 8, length: 20 };

        try {
            Array.prototype.forEach.call(obj, callbackfn);
            return false;
        } catch (ex) {
            return ex instanceof Error && !accessed;
        }
    }
runTestCase(testcase);
