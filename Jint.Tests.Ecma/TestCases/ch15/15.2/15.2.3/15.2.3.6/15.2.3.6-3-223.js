/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-223.js
 * @description Object.defineProperty - 'Attributes' is the Math object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        try {
            Math.get = function () {
                return "mathGetProperty";
            };

            Object.defineProperty(obj, "property", Math);

            return obj.property === "mathGetProperty";
        } finally {
            delete Math.get;
        }
    }
runTestCase(testcase);
