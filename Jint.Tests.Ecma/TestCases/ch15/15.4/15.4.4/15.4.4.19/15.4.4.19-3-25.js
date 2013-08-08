/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-25.js
 * @description Array.prototype.map - value of 'length' is a negative non-integer, ensure truncation occurs in the proper direction
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val < 10;
        }

        var obj = {
            0: 11,
            1: 9,
            length: -4294967294.5
        };

        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr.length === 2;
    }
runTestCase(testcase);
