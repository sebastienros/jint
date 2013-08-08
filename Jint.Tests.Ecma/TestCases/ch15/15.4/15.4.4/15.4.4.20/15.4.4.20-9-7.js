/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-7.js
 * @description Array.prototype.filter stops calling callbackfn once the array is deleted during the call
 */


function testcase() {
        var o = new Object();
        o.srcArr = [1, 2, 3, 4, 5];

        function callbackfn(val, idx, obj) {
            delete o.srcArr;
            if (val > 0)
                return true;
            else
                return false;
        }

        var resArr = o.srcArr.filter(callbackfn);
        return resArr.length === 5 && typeof o.srcArr === "undefined";
    }
runTestCase(testcase);
