/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-24.js
 * @description Array.prototype.map - value of 'length' is a positive non-integer, ensure truncation occurs in the proper direction
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val < 10;
        }

        var obj = {
            0: 11,
            1: 9,
            length: 2.685
        };

        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr.length === 2;
    }
runTestCase(testcase);
