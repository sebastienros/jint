/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-9.js
 * @description Array.prototype.forEach - value of 'length' is a number (value is -Infinity)
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 9, length: -Infinity };

        Array.prototype.forEach.call(obj, callbackfn);

        return !accessed;
    }
runTestCase(testcase);
