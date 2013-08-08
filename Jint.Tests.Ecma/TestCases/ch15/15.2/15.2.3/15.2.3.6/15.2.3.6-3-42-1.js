/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-42-1.js
 * @description Object.defineProperty - 'Attributes' is an Error object that uses Object's [[Get]] method to access the 'enumerable' property of prototype object (8.10.5 step 3.a)
 */


function testcase() {
        var obj = {};
        var accessed = false;
        try {
            Error.prototype.enumerable = true;
            var errObj = new Error();

            Object.defineProperty(obj, "property", errObj);

            for (var prop in obj) {
                if (prop === "property") {
                    accessed = true;
                }
            }

            return accessed;
        } finally {
            delete Error.prototype.enumerable;
        }
    }
runTestCase(testcase);
