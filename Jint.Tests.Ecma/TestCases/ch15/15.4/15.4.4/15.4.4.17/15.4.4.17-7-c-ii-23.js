/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-23.js
 * @description Array.prototype.some - callbackfn called with correct parameters (this object O is correct)
 */


function testcase() {

        var obj = { 0: 11, 1: 12, length: 2 };

        function callbackfn(val, idx, o) {
            return obj === o;
        }

        return Array.prototype.some.call(obj, callbackfn);
    }
runTestCase(testcase);
