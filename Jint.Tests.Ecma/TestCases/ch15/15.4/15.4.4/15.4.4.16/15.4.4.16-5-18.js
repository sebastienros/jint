/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-18.js
 * @description Array.prototype.every - Error Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objError = new RangeError();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objError;
        }

        return [11].every(callbackfn, objError) && accessed;
    }
runTestCase(testcase);
