/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-19.js
 * @description Array.prototype.map - applied to Function object, which implements its own property get method
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        var fun = function (a, b) {
            return a + b;
        };
        fun[0] = 12;
        fun[1] = 11;
        fun[2] = 9;

        var testResult = Array.prototype.map.call(fun, callbackfn);

        return 2 === testResult.length;
    }
runTestCase(testcase);
