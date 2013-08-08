/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-18.js
 * @description Array.prototype.some - Error object can be used as thisArg
 */


function testcase() {

        var objError = new RangeError();

        function callbackfn(val, idx, obj) {
            return this === objError;
        }

        return [11].some(callbackfn, objError);
    }
runTestCase(testcase);
