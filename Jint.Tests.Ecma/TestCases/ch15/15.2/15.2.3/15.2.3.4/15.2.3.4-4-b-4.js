/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-4.js
 * @description Object.getOwnPropertyNames - elements of the returned array are writable
 */


function testcase() {
        var obj = { "a": "a" };

        var result = Object.getOwnPropertyNames(obj);

        try {
            var beforeOverride = (result[0] === "a");
            result[0] = "b";
            var afterOverride = (result[0] === "b");

            return beforeOverride && afterOverride;
        } catch (ex) {
            return false;
        }
    }
runTestCase(testcase);
