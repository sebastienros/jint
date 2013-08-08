/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-140.js
 * @description Object.defineProperty - 'Attributes' is an Array object that uses Object's [[Get]] method to access the 'value' property  (8.10.5 step 5.a)
 */


function testcase() {
        var obj = { };

        var arrObj = [1, 2, 3];

        arrObj.value = "Array";

        Object.defineProperty(obj, "property", arrObj);

        return obj.property === "Array";
    }
runTestCase(testcase);
