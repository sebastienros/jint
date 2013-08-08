/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-8.js
 * @description Array.prototype.map - no observable effects occur if length is 0 on an Array-like object
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return val > 10;
        }

        var obj = { 0: 11, 1: 12, length: 0 };

        var testResult = Array.prototype.map.call(obj, callbackfn);

        return testResult.length === 0 && !accessed;
    }
runTestCase(testcase);
