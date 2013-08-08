/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-15.js
 * @description Array.prototype.map - Date object can be used as thisArg
 */


function testcase() {

        var objDate = new Date();

        function callbackfn(val, idx, obj) {
            return this === objDate;
        }

        var testResult = [11].map(callbackfn, objDate);
        return testResult[0] === true;
    }
runTestCase(testcase);
