/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-7.js
 * @description Object.freeze - 'P' is own named property of an Arguments object that implements its own [[GetOwnProperty]]
 */


function testcase() {
        var argObj = (function () { return arguments; }());

        argObj.foo = 10; // default [[Configurable]] attribute value of foo: true

        Object.freeze(argObj);

        var desc = Object.getOwnPropertyDescriptor(argObj, "foo");

        delete argObj.foo;
        return argObj.foo === 10 && desc.configurable === false && desc.writable === false;
    }
runTestCase(testcase);
