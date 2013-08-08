/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-3.js
 * @description Array.prototype.some - return value of callbackfn is a boolean (value is false)
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return false;
        }

        var obj = { 0: 11, length: 2 };

        return !Array.prototype.some.call(obj, callbackfn) && accessed;
    }
runTestCase(testcase);
