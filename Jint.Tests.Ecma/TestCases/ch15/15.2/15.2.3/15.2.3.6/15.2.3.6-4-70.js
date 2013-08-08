/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-70.js
 * @description Object.defineProperty - desc.value and name.value are two boolean values with different values (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        obj.foo = true; // default value of attributes: writable: true, configurable: true, enumerable: true

        Object.defineProperty(obj, "foo", { value: false });
        return dataPropertyAttributesAreCorrect(obj, "foo", false, true, true, true);
    }
runTestCase(testcase);
