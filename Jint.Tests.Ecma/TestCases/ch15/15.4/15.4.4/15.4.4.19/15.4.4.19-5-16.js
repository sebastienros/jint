/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-16.js
 * @description Array.prototype.map - RegExp object can be used as thisArg
 */


function testcase() {

        var objRegExp = new RegExp();

        function callbackfn(val, idx, obj) {
            return this === objRegExp;
        }

        var testResult = [11].map(callbackfn, objRegExp);
        return testResult[0] === true;
    }
runTestCase(testcase);
