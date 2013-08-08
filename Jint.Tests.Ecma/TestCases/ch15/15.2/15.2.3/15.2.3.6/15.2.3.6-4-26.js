/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-26.js
 * @description Object.defineProperty - 'name' is own accessor property (8.12.9 step 1)
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "property", {
            get: function () {
                return 11;
            },
            configurable: false
        });

        try {
            Object.defineProperty(obj, "property", {
                get: function () {
                    return 12;
                },
                configurable: true
            });
            return false;
        } catch (e) {
            return e instanceof TypeError && obj.property === 11;
        }
    }
runTestCase(testcase);
