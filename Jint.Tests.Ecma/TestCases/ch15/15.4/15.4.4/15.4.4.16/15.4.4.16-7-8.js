/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-8.js
 * @description Array.prototype.every - no observable effects occur if len is 0
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return val > 10;
        }

        var obj = { 0: 11, 1: 12, length: 0 };

        return Array.prototype.every.call(obj, callbackfn) && !accessed;
    }
runTestCase(testcase);
