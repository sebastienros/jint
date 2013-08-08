/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-12.js
 * @description Array.prototype.map - Boolean object can be used as thisArg
 */


function testcase() {

        var objBoolean = new Boolean();

        function callbackfn(val, idx, obj) {
            return this === objBoolean;
        }

        var testResult = [11].map(callbackfn, objBoolean);
        return testResult[0] === true;
    }
runTestCase(testcase);
