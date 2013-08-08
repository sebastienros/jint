/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-386.js
 * @description ES5 Attributes - [[Value]] attribute of data property is an Array object
 */


function testcase() {
        var obj = {};
        var arrObj = [];

        Object.defineProperty(obj, "prop", {
            value: arrObj
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.prop === arrObj && desc.value === arrObj;
    }
runTestCase(testcase);
