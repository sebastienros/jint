/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-8.js
 * @description Array.prototype.forEach applied to String object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = obj instanceof String;
        }

        var obj = new String("abc");
        Array.prototype.forEach.call(obj, callbackfn);

        return result;
    }
runTestCase(testcase);
