/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-36-1.js
 * @description Object.defineProperty - 'Attributes' is a Boolean object that uses Object's [[Get]] method to access the 'enumerable' property of prototype object (8.10.5 step 3.a)
 */


function testcase() {
        var obj = {};
        var accessed = false;
        try {
            Boolean.prototype.enumerable = true;
            var boolObj = new Boolean(true);

            Object.defineProperty(obj, "property", boolObj);

            for (var prop in obj) {
                if (prop === "property") {
                    accessed = true;
                }
            }

            return accessed;
        } finally {
            delete Boolean.prototype.enumerable;
        }
    }
runTestCase(testcase);
