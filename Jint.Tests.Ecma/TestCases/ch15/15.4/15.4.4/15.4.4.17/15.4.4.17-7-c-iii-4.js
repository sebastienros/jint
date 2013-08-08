/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-4.js
 * @description Array.prototype.some - return value of callbackfn is a boolean (value is true)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = { 0: 11, length: 2 };

        return Array.prototype.some.call(obj, callbackfn);
    }
runTestCase(testcase);
