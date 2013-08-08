/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-7.js
 * @description Array.prototype.forEach applied to string primitive
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = obj instanceof String;
        }

        Array.prototype.forEach.call("abc", callbackfn);
        return result;
    }
runTestCase(testcase);
