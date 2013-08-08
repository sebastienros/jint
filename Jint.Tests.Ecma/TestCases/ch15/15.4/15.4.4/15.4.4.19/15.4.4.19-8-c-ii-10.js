/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-10.js
 * @description Array.prototype.map - callbackfn is called with 1 formal parameter
 */


function testcase() {

        function callbackfn(val) {
            return val > 10;
        }

        var testResult = [11].map(callbackfn);

        return testResult[0] === true;
    }
runTestCase(testcase);
