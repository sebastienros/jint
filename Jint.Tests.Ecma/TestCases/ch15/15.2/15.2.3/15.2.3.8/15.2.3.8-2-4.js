/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-4.js
 * @description Object.seal - non-enumerable own property of 'O' is sealed
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: 10,
            enumerable: false,
            configurable: true
        });
        var preCheck = Object.isExtensible(obj);
        Object.seal(obj);

        var beforeDeleted = obj.hasOwnProperty("foo");
        delete obj.foo;
        var afterDeleted = obj.hasOwnProperty("foo");

        return preCheck && beforeDeleted && afterDeleted;
    }
runTestCase(testcase);
