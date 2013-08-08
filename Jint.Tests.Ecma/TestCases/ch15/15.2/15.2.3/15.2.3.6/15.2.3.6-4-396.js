/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-396.js
 * @description ES5 Attributes - [[Value]] attribute of data property is NaN
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "prop", {
            value: NaN
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.prop !== obj.prop && desc.value !== desc.value;
    }
runTestCase(testcase);
