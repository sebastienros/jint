/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-13.js
 * @description Array.prototype.map - callbackfn that uses arguments object to get parameter value
 */


function testcase() {

        function callbackfn() {
            return arguments[2][arguments[1]] === arguments[0];
        }

        var testResult = [11].map(callbackfn);

        return testResult[0] === true;
    }
runTestCase(testcase);
