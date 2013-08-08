/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-23.js
 * @description Array.prototype.map - number primitive can be used as thisArg
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this.valueOf() === 101;
        }

        var testResult = [11].map(callbackfn, 101);
        return testResult[0] === true;
    }
runTestCase(testcase);
