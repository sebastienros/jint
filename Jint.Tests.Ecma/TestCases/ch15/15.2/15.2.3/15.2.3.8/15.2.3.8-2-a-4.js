/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-4.js
 * @description Object.seal - 'P' is own accessor property
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "foo", {
            get: function () {
                return 10;
            },
            configurable: true
        });
        var preCheck = Object.isExtensible(obj);
        Object.seal(obj);

        delete obj.foo;
        return preCheck && obj.foo === 10;
    }
runTestCase(testcase);
