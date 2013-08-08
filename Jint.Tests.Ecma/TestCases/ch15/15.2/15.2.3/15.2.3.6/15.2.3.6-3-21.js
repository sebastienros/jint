/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-21.js
 * @description Object.defineProperty - 'enumerable' property in 'Attributes' is not present (8.10.5 step 3)
 */


function testcase() {

        var obj = {};

        var attr = {};
        var accessed = false;
        Object.defineProperty(obj, "property", attr);

        for (var prop in obj) {
            if (prop === "property") {
                accessed = true;
            }
        }
        return !accessed;
    }
runTestCase(testcase);
