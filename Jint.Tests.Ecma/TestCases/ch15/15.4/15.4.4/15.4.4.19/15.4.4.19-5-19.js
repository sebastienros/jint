/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-19.js
 * @description Array.prototype.map - the Arguments object can be used as thisArg
 */


function testcase() {

        var arg;

        function callbackfn(val, idx, obj) {
            return this === arg;
        }

        arg = (function () {
            return arguments;
        }(1, 2, 3));

        var testResult = [11].map(callbackfn, arg);
        return testResult[0] === true;
    }
runTestCase(testcase);
