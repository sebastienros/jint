/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-4.js
 * @description Object.freeze - 'P' is own accessor property
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "foo", {
            get: function () {
                return 10;
            },
            configurable: true
        });

        Object.freeze(obj);

        var desc = Object.getOwnPropertyDescriptor(obj, "foo");

        delete obj.foo;
        return obj.foo === 10 && desc.configurable === false;
    }
runTestCase(testcase);
