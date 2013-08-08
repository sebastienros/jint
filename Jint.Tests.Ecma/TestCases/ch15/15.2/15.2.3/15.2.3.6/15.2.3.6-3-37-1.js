/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-37-1.js
 * @description Object.defineProperty - 'Attributes' is a Number object that uses Object's [[Get]] method to access the 'enumerable' property of prototype object (8.10.5 step 3.a)
 */


function testcase() {
        var obj = {};
        var accessed = false;
        try {
            Number.prototype.enumerable = true;
            var numObj = new Number(-2);

            Object.defineProperty(obj, "property", numObj);

            for (var prop in obj) {
                if (prop === "property") {
                    accessed = true;
                }
            }

            return accessed;
        } finally {
            delete Number.prototype.enumerable;
        }
    }
runTestCase(testcase);
