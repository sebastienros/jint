/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-63.js
 * @description Object.defineProperty - both desc.value and name.value are NaN (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { value: NaN });

        Object.defineProperty(obj, "foo", { value: NaN });

        if (!isNaN(obj.foo)) {
            return false;
        }

        obj.foo = "verifyValue";
        if (obj.foo === "verifyValue") {
            return false;
        }

        for (var prop in obj) {
            if (obj.hasOwnProperty(prop) && prop === "foo") {
                return false;
            }
        }

        delete obj.foo;
        if (!obj.hasOwnProperty("foo")) {
            return false;
        }

        return true;
    }
runTestCase(testcase);
