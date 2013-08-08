/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-102.js
 * @description Object.defineProperty - 'name' and 'desc' are data properties, desc.value is present and name.value is undefined (8.12.9 step 12)
 */


function testcase() {

        var obj = {};

        obj.foo = undefined; // default value of attributes: writable: true, configurable: true, enumerable: true

        Object.defineProperty(obj, "foo", { value: 100 });
        return dataPropertyAttributesAreCorrect(obj, "foo", 100, true, true, true);
    }
runTestCase(testcase);
