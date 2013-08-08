/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-382.js
 * @description ES5 Attributes - [[Value]] attribute of data property is a number
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "prop", {
            value: 1001
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.prop === 1001 && desc.value === 1001;
    }
runTestCase(testcase);
