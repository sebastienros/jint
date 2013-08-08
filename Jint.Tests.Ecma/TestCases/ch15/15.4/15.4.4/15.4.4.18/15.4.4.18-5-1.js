/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-1.js
 * @description Array.prototype.forEach - thisArg is passed
 */


function testcase() {
        this._15_4_4_18_5_1 = false;
        var _15_4_4_18_5_1 = true;
        var result;
        function callbackfn(val, idx, obj) {
            result = this._15_4_4_18_5_1;
        }
        var arr = [1];
        arr.forEach(callbackfn)
        return !result;
    }
runTestCase(testcase);
