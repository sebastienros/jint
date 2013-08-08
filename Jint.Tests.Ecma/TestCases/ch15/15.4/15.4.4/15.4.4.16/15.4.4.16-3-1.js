/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-1.js
 * @description Array.prototype.every - value of 'length' is undefined
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return val > 10;
        }

        var obj = { 0: 9, length: undefined };

        return Array.prototype.every.call(obj, callbackfn) && !accessed;
    }
runTestCase(testcase);
