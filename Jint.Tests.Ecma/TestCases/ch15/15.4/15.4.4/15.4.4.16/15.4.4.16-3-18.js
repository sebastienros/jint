/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-18.js
 * @description Array.prototype.every - value of 'length' is a string that can't convert to a number
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return val > 10;
        }

        var obj = { 0: 9, 1: 8, length: "two" };

        return Array.prototype.every.call(obj, callbackfn) && !accessed;
    }
runTestCase(testcase);
