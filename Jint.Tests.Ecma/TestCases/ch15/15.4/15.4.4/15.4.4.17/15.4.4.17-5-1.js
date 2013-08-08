/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-1.js
 * @description Array.prototype.some - thisArg is passed
 */


function testcase() {
        this._15_4_4_17_5_1 = false;
        var _15_4_4_17_5_1 = true;

        function callbackfn(val, idx, obj) {
            return this._15_4_4_17_5_1;
        }
        var arr = [1];
        return !arr.some(callbackfn);
    }
runTestCase(testcase);
