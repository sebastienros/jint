/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-c-1.js
 * @description Object.freeze - The [[Configurable]] attribute of own data property of 'O' is set to false while other attributes are unchanged
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: 10,
            writable: false,
            enumerable: true,
            configurable: true
        });

        Object.freeze(obj);
        var desc = Object.getOwnPropertyDescriptor(obj, "foo");

        return dataPropertyAttributesAreCorrect(obj, "foo", 10, false, true, false) &&
            desc.configurable === false && desc.writable === false;
    }
runTestCase(testcase);
