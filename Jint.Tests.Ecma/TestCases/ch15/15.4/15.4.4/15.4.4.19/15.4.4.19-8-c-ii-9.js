/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-9.js
 * @description Array.prototype.map - callbackfn with 0 formal parameter
 */


function testcase() {

        function callbackfn() {
            return true;
        }

        var testResult = [11].map(callbackfn);

        return testResult[0] === true;
    }
runTestCase(testcase);
