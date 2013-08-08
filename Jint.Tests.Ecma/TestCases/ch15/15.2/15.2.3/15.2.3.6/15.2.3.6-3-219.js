/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-219.js
 * @description Object.defineProperty - 'Attributes' is an Array object that uses Object's [[Get]] method to access the 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var arrObj = [];

        arrObj.get = function () {
            return "arrayGetProperty";
        };

        Object.defineProperty(obj, "property", arrObj);

        return obj.property === "arrayGetProperty";
    }
runTestCase(testcase);
