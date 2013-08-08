/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-4.js
 * @description Object.isFrozen - 'P' is own accessor property
 */


function testcase() {

        var obj = {};
        Object.defineProperty(obj, "foo", {
            get: function () {
                return 9;
            },
            configurable: true
        });

        Object.preventExtensions(obj);
        return !Object.isFrozen(obj);
    }
runTestCase(testcase);
