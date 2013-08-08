/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-207.js
 * @description Object.defineProperty - 'get' property in 'Attributes' is own data property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};
        var attributes = {
            get: function () {
                return "ownDataProperty";
            }
        };

        Object.defineProperty(obj, "property", attributes);

        return obj.property === "ownDataProperty";
    }
runTestCase(testcase);
