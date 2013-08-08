/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-391.js
 * @description ES5 Attributes - [[Value]] attribute of data property is an Error object
 */


function testcase() {
        var obj = {};
        var errObj = new Error();

        Object.defineProperty(obj, "prop", {
            value: errObj
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.prop === errObj && desc.value === errObj;
    }
runTestCase(testcase);
