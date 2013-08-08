/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-41.js
 * @description Object.defineProperty - 'Attributes' is the JSON object that uses Object's [[Get]] method to access the 'enumerable' property (8.10.5 step 3.a)
 */


function testcase() {
        var obj = {};
        var accessed = false;

        try {
            JSON.enumerable = true;

            Object.defineProperty(obj, "property", JSON);

            for (var prop in obj) {
                if (prop === "property") {
                    accessed = true;
                }
            }

            return accessed;
        } finally {
            delete JSON.enumerable;
        }
    }
runTestCase(testcase);
