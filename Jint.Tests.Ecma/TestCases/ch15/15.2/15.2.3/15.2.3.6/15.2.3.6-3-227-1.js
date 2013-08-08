/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-227-1.js
 * @description Object.defineProperty - 'Attributes' is an Error object that uses Object's [[Get]] method to access the 'get' property of prototype object (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};
        try {
            Error.prototype.get = function () {
                return "errorGetProperty";
            };
            var errObj = new Error();

            Object.defineProperty(obj, "property", errObj);

            return obj.property === "errorGetProperty";
        } finally {
            delete Error.prototype.get;
        }
    }
runTestCase(testcase);
