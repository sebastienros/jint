/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-11.js
 * @description Array.prototype.map - String object can be used as thisArg
 */


function testcase() {

        var objString = new String();

        function callbackfn(val, idx, obj) {
            return this === objString;
        }

        var testResult = [11].map(callbackfn, objString);
        return testResult[0] === true;
    }
runTestCase(testcase);
