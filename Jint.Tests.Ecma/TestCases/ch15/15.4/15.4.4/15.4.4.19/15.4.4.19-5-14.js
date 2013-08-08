/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-14.js
 * @description Array.prototype.map - the Math object can be used as thisArg
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this === Math;
        }

        var testResult = [11].map(callbackfn, Math);
        return testResult[0] === true;
    }
runTestCase(testcase);
