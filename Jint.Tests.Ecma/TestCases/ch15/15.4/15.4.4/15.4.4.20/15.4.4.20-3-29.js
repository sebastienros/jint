/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-29.js
 * @description Array.prototype.filter - value of 'length' is boundary value (2^32 + 1)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = {
            0: 11,
            1: 9,
            length: 4294967297
        };

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
