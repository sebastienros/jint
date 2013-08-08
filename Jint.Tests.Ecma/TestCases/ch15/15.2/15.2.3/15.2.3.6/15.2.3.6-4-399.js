/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-399.js
 * @description ES5 Attributes - [[Value]] attribute of data property is the global object
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "prop", {
            value: fnGlobalObject()
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.prop === fnGlobalObject() && desc.value === fnGlobalObject();
    }
runTestCase(testcase);
