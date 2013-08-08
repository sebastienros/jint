/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-30.js
 * @description Object.defineProperty - 'name' is own accessor property without a get function (8.12.9 step 1)
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, "foo", {
            set: function () { },
            configurable: false
        });

        try {
            Object.defineProperty(obj, "foo", {
                configurable: true
            });
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);
