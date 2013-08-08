/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-38.js
 * @description Object.defineProperties - 'P' exists in 'O', test 'P' makes no change if 'desc' is generic descriptor without any attribute (8.12.9 step 5)
 */


function testcase() {

        var obj = {};
        obj.foo = 100; // default value of attributes: writable: true, configurable: true, enumerable: true

        Object.defineProperties(obj, { foo: {} });
        return dataPropertyAttributesAreCorrect(obj, "foo", 100, true, true, true);
    }
runTestCase(testcase);
