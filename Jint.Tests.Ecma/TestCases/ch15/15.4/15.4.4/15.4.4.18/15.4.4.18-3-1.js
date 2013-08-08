/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-1.js
 * @description Array.prototype.forEach - value of 'length' is undefined
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 0, 1: 1, length: undefined };

        Array.prototype.forEach.call(obj, callbackfn);

        return !accessed;
    }
runTestCase(testcase);
