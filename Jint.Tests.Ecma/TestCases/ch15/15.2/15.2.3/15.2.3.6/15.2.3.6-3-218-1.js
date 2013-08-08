/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-218-1.js
 * @description Object.defineProperty - 'Attributes' is a Function object which implements its own [[Get]] method to access the 'get' property of prototype object (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};
        try {
            Function.prototype.get = function () {
                return "functionGetProperty";
            };
            var funObj = function () { };

            Object.defineProperty(obj, "property", funObj);

            return obj.property === "functionGetProperty";
        } finally {
            delete Function.prototype.get;
        }
    }
runTestCase(testcase);
