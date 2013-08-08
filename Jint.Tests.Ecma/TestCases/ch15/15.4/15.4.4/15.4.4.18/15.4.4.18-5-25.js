/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-25.js
 * @description Array.prototype.forEach - thisArg not passed
 */


function testcase() {
        function innerObj() {
            this._15_4_4_18_5_25 = true;
            var _15_4_4_18_5_25 = false;
            var result;
            function callbackfn(val, idx, obj) {
                result = this._15_4_4_18_5_25;
            }
            var arr = [1];
            arr.forEach(callbackfn)
            this.retVal = !result;
        }
        return new innerObj().retVal;
    }
runTestCase(testcase);
