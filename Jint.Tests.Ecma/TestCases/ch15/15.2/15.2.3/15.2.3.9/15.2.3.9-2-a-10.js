/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-10.js
 * @description Object.freeze - 'P' is own named property of an Array object that uses Object's [[GetOwnProperty]]
 */


function testcase() {
        var arrObj = [];

        arrObj.foo = 10; // default [[Configurable]] attribute value of foo: true

        Object.freeze(arrObj);

        var desc = Object.getOwnPropertyDescriptor(arrObj, "foo");

        delete arrObj.foo;
        return arrObj.foo === 10 && desc.configurable === false && desc.writable === false;
    }
runTestCase(testcase);
