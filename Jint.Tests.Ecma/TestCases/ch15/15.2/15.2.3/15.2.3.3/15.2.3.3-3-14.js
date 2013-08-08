/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-14.js
 * @description Object.getOwnPropertyDescriptor applied to a String object which implements its own property get method
 */


function testcase() {

        var str = new String("123");

        var desc = Object.getOwnPropertyDescriptor(str, "2");

        return desc.value === "3";
    }
runTestCase(testcase);
