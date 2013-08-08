/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-15.js
 * @description Array.prototype.map - 'length' is a string containing an exponential number
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val < 10;
        }

        var obj = { 0: 11, 1: 9, 2: 12, length: "2E0" };

        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr.length === 2;
    }
runTestCase(testcase);
