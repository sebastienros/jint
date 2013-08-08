/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-225.js
 * @description Object.defineProperty - 'Attributes' is a RegExp object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var regObj = new RegExp();

        regObj.get = function () {
            return "regExpGetProperty";
        };

        Object.defineProperty(obj, "property", regObj);

        return obj.property === "regExpGetProperty";
    }
runTestCase(testcase);
