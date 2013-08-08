/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-1.js
 * @description Object.isFrozen - 'P' is own data property
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: 12,
            writable: true,
            configurable: false
        });

        Object.preventExtensions(obj);

        return !Object.isFrozen(obj);
    }
runTestCase(testcase);
