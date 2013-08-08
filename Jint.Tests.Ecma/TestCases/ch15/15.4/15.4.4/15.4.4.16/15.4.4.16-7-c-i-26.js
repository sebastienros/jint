/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-26.js
 * @description Array.prototype.every - This object is the Arguments object which implements its own property get method (number of arguments equals number of parameters)
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            if (idx === 0) {
                return val === 11;
            } else if (idx === 1) {
                return val === 9;
            } else {
                return false;
            }
        }

        var func = function (a, b) {
            return Array.prototype.every.call(arguments, callbackfn);
        };

        return func(11, 9) && called === 2;
    }
runTestCase(testcase);
