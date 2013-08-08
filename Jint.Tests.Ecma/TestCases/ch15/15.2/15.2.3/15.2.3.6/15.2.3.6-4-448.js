/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-448.js
 * @description ES5 Attributes - success to update the accessor property ([[Get]] is undefined, [[Set]] is undefined, [[Enumerable]] is false, [[Configurable]] is true) to a data property
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "prop", {
            get: undefined,
            set: undefined,
            enumerable: false,
            configurable: true
        });
        var desc1 = Object.getOwnPropertyDescriptor(obj, "prop");

        Object.defineProperty(obj, "prop", {
            value: 1001
        });
        var desc2 = Object.getOwnPropertyDescriptor(obj, "prop");

        return desc1.hasOwnProperty("get") && desc2.hasOwnProperty("value");
    }
runTestCase(testcase);
