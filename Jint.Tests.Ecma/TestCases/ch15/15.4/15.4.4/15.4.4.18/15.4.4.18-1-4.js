/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-4.js
 * @description Array.prototype.forEach applied to Boolean object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = obj instanceof Boolean;
        }

        var obj = new Boolean(true);
        obj.length = 2;
        obj[0] = 11;
        obj[1] = 12;

        Array.prototype.forEach.call(obj, callbackfn);
        return result;
    }
runTestCase(testcase);
