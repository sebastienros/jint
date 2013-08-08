/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-224-1.js
 * @description Object.defineProperty - 'Attributes' is a Date object that uses Object's [[Get]] method to access the 'get' property of prototype object (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};
        try {
            Date.prototype.get = function () {
                return "dateGetProperty";
            };
            var dateObj = new Date();

            Object.defineProperty(obj, "property", dateObj);

            return obj.property === "dateGetProperty";
        } finally {
            delete Date.prototype.get;
        }
    }
runTestCase(testcase);
