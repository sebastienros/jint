/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-221-1.js
 * @description Object.defineProperty - 'Attributes' is a Boolean object that uses Object's [[Get]] method to access the 'get' property of prototype object (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};
        try {
            Boolean.prototype.get = function () {
                return "booleanGetProperty";
            };
            var boolObj = new Boolean(true);

            Object.defineProperty(obj, "property", boolObj);

            return obj.property === "booleanGetProperty";
        } finally {
            delete Boolean.prototype.get;
        }
    }
runTestCase(testcase);
