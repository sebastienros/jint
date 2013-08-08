/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-9.js
 * @description Array.prototype.forEach applied to Function object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = obj instanceof Function;
        }

        var obj = function (a, b) {
            return a + b;
        };
        obj[0] = 11;
        obj[1] = 9;

        Array.prototype.forEach.call(obj, callbackfn);
        return result;
    }
runTestCase(testcase);
