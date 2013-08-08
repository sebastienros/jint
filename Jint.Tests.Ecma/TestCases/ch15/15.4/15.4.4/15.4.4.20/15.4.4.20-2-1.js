/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-1.js
 * @description Array.prototype.filter applied to Array-like object, 'length' is own data property
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return obj.length === 2;
        }

        var obj = {
            0: 12,
            1: 11,
            2: 9,
            length: 2
        };

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 2;
    }
runTestCase(testcase);
