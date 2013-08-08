/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-1.js
 * @description Object.freeze - 'P' is own data property
 */


function testcase() {
        var obj = {};

        obj.foo = 10; // default [[Configurable]] attribute value of foo: true

        Object.freeze(obj);

        var desc = Object.getOwnPropertyDescriptor(obj, "foo");

        delete obj.foo;
        return obj.foo === 10 && desc.configurable === false && desc.writable === false;
    }
runTestCase(testcase);
