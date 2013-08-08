/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-1.js
 * @description Array.prototype.forEach - 'length' is own data property on an Array-like object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = (obj.length === 2);
        }

        var obj = {
            0: 12,
            1: 11,
            2: 9,
            length: 2
        };

        Array.prototype.forEach.call(obj, callbackfn);
        return result;
    }
runTestCase(testcase);
