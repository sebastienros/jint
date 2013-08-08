/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-26.js
 * @description Array.prototype.some - This object is the Arguments object which implements its own property get method (number of arguments equals number of parameters)
 */


function testcase() {

        var firstResult = false;
        var secondResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                firstResult = (val === 11);
                return false;
            } else if (idx === 1) {
                secondResult = (val === 9);
                return false;
            } else {
                return true;
            }
        }

        var func = function (a, b) {
            return Array.prototype.some.call(arguments, callbackfn);
        };

        return !func(11, 9) && firstResult && secondResult;
    }
runTestCase(testcase);
