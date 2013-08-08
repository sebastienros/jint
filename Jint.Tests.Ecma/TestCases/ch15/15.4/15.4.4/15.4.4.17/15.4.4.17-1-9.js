/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-9.js
 * @description Array.prototype.some applied to Function object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof Function;
        }

        var obj = function (a, b) {
            return a + b;
        };
        obj[0] = 11;
        obj[1] = 9;
        return Array.prototype.some.call(obj, callbackfn);
    }
runTestCase(testcase);
