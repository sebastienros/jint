/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-232.js
 * @description Object.defineProperty - value of 'get' property in 'Attributes' is a function (8.10.5 step 7.b)
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "property", {
            get: function () {
                return "getFunction";
            }
        });

        return obj.hasOwnProperty("property") && obj.property === "getFunction";
    }
runTestCase(testcase);
