/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-14.js
 * @description Array.prototype.forEach applied to Error object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = obj instanceof Error;
        }

        var obj = new Error();
        obj.length = 1;
        obj[0] = 1;

        Array.prototype.forEach.call(obj, callbackfn);
        return result;
    }
runTestCase(testcase);
