/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-228-1.js
 * @description Object.defineProperty - 'Attributes' is an Arguments object which implements its own [[Get]] method to access the 'get' property of prototype object (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};
        try {
            Object.prototype.get = function () {
                return "argumentGetProperty";
            };
            var argObj = (function () { return arguments; })();

            Object.defineProperty(obj, "property", argObj);

            return obj.property === "argumentGetProperty";
        } finally {
            delete Object.prototype.get;
        }
    }
runTestCase(testcase);
