/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-7.js
 * @description Array.prototype.every - unhandled exceptions happened in callbackfn terminate iteration
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            if (called === 1) {
                throw new Error("Exception occurred in callbackfn");
            }
            return true;
        }

        var obj = { 0: 11, 4: 10, 10: 8, length: 20 };

        try {
            Array.prototype.every.call(obj, callbackfn);
            return false;
        } catch (ex) {
            return 1 === called;
        }
    }
runTestCase(testcase);
