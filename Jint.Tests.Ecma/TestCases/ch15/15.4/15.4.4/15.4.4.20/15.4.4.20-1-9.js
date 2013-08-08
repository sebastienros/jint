/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-9.js
 * @description Array.prototype.filter applied to Function object
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

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr[0] === 11 && newArr[1] === 9;
    }
runTestCase(testcase);
