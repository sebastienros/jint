/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-389.js
 * @description ES5 Attributes - [[Value]] attribute of data property is a Boolean Object
 */


function testcase() {
        var obj = {};
        var boolObj = new Boolean();

        Object.defineProperty(obj, "prop", {
            value: boolObj
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.prop === boolObj && desc.value === boolObj;
    }
runTestCase(testcase);
