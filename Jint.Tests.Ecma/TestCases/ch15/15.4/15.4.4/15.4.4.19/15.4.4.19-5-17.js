/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-17.js
 * @description Array.prototype.map - the JSON object can be used as thisArg
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this === JSON;
        }

        var testResult = [11].map(callbackfn, JSON);
        return testResult[0] === true;
    }
runTestCase(testcase);
