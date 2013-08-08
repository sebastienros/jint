/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-21.js
 * @description Array.prototype.every - callbackfn called with correct parameters (kValue is correct)
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            if (idx === 0) {
                return val === 11;
            }

            if (idx === 1) {
                return val === 12;
            }

        }

        var obj = { 0: 11, 1: 12, length: 2 };

        return Array.prototype.every.call(obj, callbackfn) && accessed;
    }
runTestCase(testcase);
