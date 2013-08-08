/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-18.js
 * @description Array.prototype.forEach - Error Object can be used as thisArg
 */


function testcase() {

        var result = false;
        var objError = new RangeError();

        function callbackfn(val, idx, obj) {
            result = (this === objError);
        }

        [11].forEach(callbackfn, objError);
        return result;
    }
runTestCase(testcase);
