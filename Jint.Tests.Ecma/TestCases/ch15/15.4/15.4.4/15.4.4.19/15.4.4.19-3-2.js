/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-2.js
 * @description Array.prototype.map on an Array-like object if 'length' is 1 (length overridden to true(type conversion))
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        var obj = { 0: 11, length: true };

        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr.length === 1;
    }
runTestCase(testcase);
