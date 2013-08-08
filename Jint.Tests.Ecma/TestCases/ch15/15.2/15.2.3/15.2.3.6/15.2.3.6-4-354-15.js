/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-15.js
 * @description Object.defineProperty - Named property 'P' with attributes [[Writable]]: false, [[Enumerable]]: true, [[Configurable]]: true is non-writable using simple assignment, 'A' is an Array object
 */


function testcase() {
        var obj = [];

        Object.defineProperty(obj, "prop", {
            value: 2010,
            writable: false,
            enumerable: true,
            configurable: true
        });
        var verifyValue = (obj.prop === 2010);
        obj.prop = 1001;

        return verifyValue && obj.prop === 2010;
    }
runTestCase(testcase);
