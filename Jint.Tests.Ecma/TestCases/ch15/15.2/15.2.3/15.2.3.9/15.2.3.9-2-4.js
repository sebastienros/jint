/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-4.js
 * @description Object.freeze - Non-enumerable own properties of 'O' are frozen
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: 10,
            enumerable: false,
            configurable: true
        });

        Object.freeze(obj);

        var desc = Object.getOwnPropertyDescriptor(obj, "foo");

        var beforeDeleted = obj.hasOwnProperty("foo");
        delete obj.foo;
        var afterDeleted = obj.hasOwnProperty("foo");

        return beforeDeleted && afterDeleted && desc.configurable === false && desc.writable === false;
    }
runTestCase(testcase);
