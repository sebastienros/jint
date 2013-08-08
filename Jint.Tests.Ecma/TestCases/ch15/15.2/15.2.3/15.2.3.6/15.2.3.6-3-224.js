/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-224.js
 * @description Object.defineProperty - 'Attributes' is a Date object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var dateObj = new Date();

        dateObj.get = function () {
            return "dateGetProperty";
        };

        Object.defineProperty(obj, "property", dateObj);

        return obj.property === "dateGetProperty";
    }
runTestCase(testcase);
