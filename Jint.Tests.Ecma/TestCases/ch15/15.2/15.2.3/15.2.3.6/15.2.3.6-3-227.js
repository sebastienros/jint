/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-227.js
 * @description Object.defineProperty - 'Attributes' is an Error object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var errObj = new Error();

        errObj.get = function () {
            return "errorGetProperty";
        };

        Object.defineProperty(obj, "property", errObj);

        return obj.property === "errorGetProperty";
    }
runTestCase(testcase);
