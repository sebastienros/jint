/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-10.js
 * @description Array.prototype.every - return value of callbackfn is a number (value is Infinity)
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return Infinity;
        }

        return [11].every(callbackfn) && accessed;
    }
runTestCase(testcase);
