/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-231.js
 * @description Object.defineProperty - value of 'get' property in 'Attributes' is undefined (8.10.5 step 7.b)
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "property", {
            get: undefined
        });

        return obj.hasOwnProperty("property") && typeof obj.property === "undefined";
    }
runTestCase(testcase);
