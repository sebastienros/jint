/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-230.js
 * @description Object.defineProperty - 'Attributes' is the global object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        try {
            fnGlobalObject().get = function () {
                return "globalGetProperty";
            };

            Object.defineProperty(obj, "property", fnGlobalObject());

            return obj.property === "globalGetProperty";
        } finally {
            delete fnGlobalObject().get;
        }
    }
runTestCase(testcase);
