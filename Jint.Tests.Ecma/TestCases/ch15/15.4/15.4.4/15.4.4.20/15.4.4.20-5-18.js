/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-18.js
 * @description Array.prototype.filter - Error Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objError = new RangeError();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objError;
        }

        var newArr = [11].filter(callbackfn, objError);

        return newArr[0] === 11 && accessed;
    }
runTestCase(testcase);
