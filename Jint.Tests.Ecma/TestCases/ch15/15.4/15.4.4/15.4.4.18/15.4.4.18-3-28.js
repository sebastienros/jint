/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-28.js
 * @description Array.prototype.forEach - value of 'length' is boundary value (2^32)
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        var obj = {
            0: 12,
            length: 4294967296
        };

        Array.prototype.forEach.call(obj, callbackfn);

        return !accessed;
    }
runTestCase(testcase);
