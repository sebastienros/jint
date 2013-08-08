/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-9.js
 * @description Array.prototype.every applied to Function object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return !(obj instanceof Function);
        }

        var obj = function (a, b) {
            return a + b;
        };
        obj[0] = 11;
        obj[1] = 9;

        return !Array.prototype.every.call(obj, callbackfn);
    }
runTestCase(testcase);
