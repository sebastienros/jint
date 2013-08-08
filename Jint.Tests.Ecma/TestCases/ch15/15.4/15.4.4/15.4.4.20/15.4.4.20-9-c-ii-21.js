/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-21.js
 * @description Array.prototype.filter - callbackfn called with correct parameters (kValue is correct)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === 11;
            }

            if (idx === 1) {
                return val === 12;
            }

            return false;
        }

        var obj = { 0: 11, 1: 12, length: 2 };
        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 2 && newArr[0] === 11 && newArr[1] === 12;
    }
runTestCase(testcase);
