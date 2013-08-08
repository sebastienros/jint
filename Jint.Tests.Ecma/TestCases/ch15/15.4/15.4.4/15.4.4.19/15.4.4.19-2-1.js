/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-1.js
 * @description Array.prototype.map - applied to Array-like object when 'length' is an own data property
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        var obj = {
            0: 12,
            1: 11,
            2: 9,
            length: 2
        };

        var testResult = Array.prototype.map.call(obj, callbackfn);

        return testResult.length === 2;
    }
runTestCase(testcase);
