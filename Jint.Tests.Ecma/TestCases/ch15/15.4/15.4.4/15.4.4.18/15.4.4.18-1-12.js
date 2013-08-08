/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-12.js
 * @description Array.prototype.forEach applied to RegExp object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = obj instanceof RegExp;
        }

        var obj = new RegExp();
        obj.length = 1;
        obj[0] = 1;

        Array.prototype.forEach.call(obj, callbackfn);
        return result;
    }
runTestCase(testcase);
