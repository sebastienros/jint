/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-9.js
 * @description Array.prototype.map - Function object can be used as thisArg
 */


function testcase() {

        var objFunction = function () { };

        function callbackfn(val, idx, obj) {
            return this === objFunction;
        }

        var testResult = [11].map(callbackfn, objFunction);
        return testResult[0] === true;
    }
runTestCase(testcase);
