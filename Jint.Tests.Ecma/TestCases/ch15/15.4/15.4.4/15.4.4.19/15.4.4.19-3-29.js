/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-29.js
 * @description Array.prototype.map - value of 'length' is boundary value (2^32 + 1)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        var obj = {
            0: 11,
            1: 9,
            length: 4294967297
        };

        var newArr = Array.prototype.map.call(obj, callbackfn);
        return newArr.length === 1;
    }
runTestCase(testcase);
