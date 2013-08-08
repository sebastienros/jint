/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-141.js
 * @description Object.defineProperty - 'Attributes' is a String object that uses Object's [[Get]] method to access the 'value' property  (8.10.5 step 5.a)
 */


function testcase() {
        var obj = { };

        var strObj = new String("abc");

        strObj.value = "String";

        Object.defineProperty(obj, "property", strObj);

        return obj.property === "String";
    }
runTestCase(testcase);
