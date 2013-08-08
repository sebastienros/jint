/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-12.js
 * @description Array.prototype.map - callbackfn is called with 3 formal parameters
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return (val > 10 && obj[idx] === val);
        }

        var testResult = [11].map(callbackfn);

        return testResult[0] === true;
    }
runTestCase(testcase);
