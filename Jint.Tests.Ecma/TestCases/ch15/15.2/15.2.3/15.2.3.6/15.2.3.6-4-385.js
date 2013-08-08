/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-385.js
 * @description ES5 Attributes - [[Value]] attribute of data property is a generic object
 */


function testcase() {
        var obj = {};
        var tempObj = { testproperty: 100 };

        Object.defineProperty(obj, "prop", {
            value: tempObj
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.prop === tempObj && desc.value === tempObj;
    }
runTestCase(testcase);
