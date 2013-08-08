/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-25.js
 * @description Array.prototype.map - This object is the Arguments object which implements its own property get method (number of arguments is less than number of parameters)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === 9;
            } else {
                return false;
            }
        }

        var func = function (a, b) {
            return Array.prototype.map.call(arguments, callbackfn);
        };

        var testResult = func(9);

        return testResult[0] === true;
    }
runTestCase(testcase);
