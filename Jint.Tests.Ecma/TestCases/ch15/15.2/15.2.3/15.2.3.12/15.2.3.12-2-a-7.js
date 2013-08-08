/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-7.js
 * @description Object.isFrozen - 'P' is own accessor property without a get function
 */


function testcase() {

        var obj = {};
        Object.defineProperty(obj, "foo", {
            set: function () { },
            configurable: true
        });

        Object.preventExtensions(obj);
        return !Object.isFrozen(obj);
    }
runTestCase(testcase);
