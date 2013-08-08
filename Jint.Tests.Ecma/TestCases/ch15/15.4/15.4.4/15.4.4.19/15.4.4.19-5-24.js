/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-24.js
 * @description Array.prototype.map - string primitive can be used as thisArg
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this.valueOf() === "abc";
        }

        var testResult = [11].map(callbackfn, "abc");
        return testResult[0] === true;
    }
runTestCase(testcase);
