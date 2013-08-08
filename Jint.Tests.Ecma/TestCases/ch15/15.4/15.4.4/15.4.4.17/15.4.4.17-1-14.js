/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-14.js
 * @description Array.prototype.some applied to Error object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof Error;
        }

        var obj = new Error();
        obj.length = 1;
        obj[0] = 1;

        return Array.prototype.some.call(obj, callbackfn);
    }
runTestCase(testcase);
