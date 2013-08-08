/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-127.js
 * @description Object.defineProperty - 'value' property in 'Attributes' is not present  (8.10.5 step 5)
 */


function testcase() {
        var obj = { };

        var attr = {
            writable: true
        };

        Object.defineProperty(obj, "property", attr);

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
