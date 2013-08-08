/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-220.js
 * @description Object.defineProperty - 'Attributes' is a String object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var strObj = new String();

        strObj.get = function () {
            return "stringGetProperty";
        };

        Object.defineProperty(obj, "property", strObj);

        return obj.property === "stringGetProperty";
    }
runTestCase(testcase);
