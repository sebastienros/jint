/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-326.js
 * @description ES5 Attributes - property ([[Writable]] is true, [[Enumerable]] is true, [[Configurable]] is true) is writable
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "prop", {
            value: 2010,
            writable: true,
            enumerable: true,
            configurable: true
        });
        var propertyDefineCorrect = (obj.prop === 2010);
        obj.prop = 1001;

        return propertyDefineCorrect && obj.prop === 1001;
    }
runTestCase(testcase);
