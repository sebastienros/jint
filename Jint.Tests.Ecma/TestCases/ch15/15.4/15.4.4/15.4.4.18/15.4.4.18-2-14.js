/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-14.js
 * @description Array.prototype.forEach applied to the Array-like object that 'length' property doesn't exist
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 11, 1: 12 };

        Array.prototype.forEach.call(obj, callbackfn);
        return !accessed;

    }
runTestCase(testcase);
