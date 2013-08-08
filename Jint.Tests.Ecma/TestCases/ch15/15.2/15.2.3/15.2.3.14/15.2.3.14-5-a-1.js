/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-a-1.js
 * @description Object.keys - 'value' attribute of element in returned array is correct.
 */


function testcase() {
        var obj = { prop1: 1 };

        var array = Object.keys(obj);

        var desc = Object.getOwnPropertyDescriptor(array, "0");

        return desc.hasOwnProperty("value") && desc.value === "prop1";
    }
runTestCase(testcase);
