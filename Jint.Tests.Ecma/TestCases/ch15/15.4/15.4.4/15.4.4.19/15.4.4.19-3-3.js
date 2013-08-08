/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-3.js
 * @description Array.prototype.map - value of 'length' is a number (value is 0)
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        var obj = { 0: 11, length: 0 };

        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr.length === 0;
    }
runTestCase(testcase);
