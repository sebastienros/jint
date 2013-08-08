/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-14.js
 * @description Array.prototype.reduce applied to Error object
 */


function testcase() {
        function callbackfn(prevVal, curVal, idx, obj) {
            return obj instanceof Error;
        }

        var obj = new Error();
        obj.length = 1;
        obj[0] = 1;

        return Array.prototype.reduce.call(obj, callbackfn, 1);
    }
runTestCase(testcase);
