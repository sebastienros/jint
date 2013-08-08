/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-8.js
 * @description Array.prototype.forEach - no observable effects occur if len is 0
 */


function testcase() {
        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 11, 1: 12, length: 0 };

        Array.prototype.forEach.call(obj, callbackfn);
        return !accessed;
    }
runTestCase(testcase);
