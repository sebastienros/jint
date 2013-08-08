/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-39.js
 * @description Object.getOwnPropertyNames - own accessor properties are pushed into the returned array
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, "a", {
            get: function () {
                return "a";
            },
            configurable: true
        });

        var result = Object.getOwnPropertyNames(obj);

        return result[0] === "a";
    }
runTestCase(testcase);
