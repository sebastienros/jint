/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-14.js
 * @description Array.prototype.map - applied to the Array-like object that 'length' property doesn't exist
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val > 10;
        }

        var obj = { 0: 11, 1: 12 };

        var testResult = Array.prototype.map.call(obj, callbackfn);

        return 0 === testResult.length;
    }
runTestCase(testcase);
