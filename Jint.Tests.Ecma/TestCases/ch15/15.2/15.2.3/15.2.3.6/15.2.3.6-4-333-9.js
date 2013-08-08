/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-333-9.js
 * @description Object.defineProperty - Named property 'P' with attributes [[Writable]]: true, [[Enumerable]]:true, [[Configurable]]:false is writable using simple assignment, 'A' is an Array Object
 */


function testcase() {
        var obj = [];

        Object.defineProperty(obj, "prop", {
            value: 2010,
            writable: true,
            enumerable: true,
            configurable: false
        });
        var verifyValue = (obj.prop === 2010);
        obj.prop = 1001;

        return verifyValue && obj.prop === 1001;
    }
runTestCase(testcase);
