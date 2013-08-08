/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-169.js
 * @description Object.defineProperty - 'Attributes' is a Number object that uses Object's [[Get]] method to access the 'writable' property  (8.10.5 step 6.a)
 */


function testcase() {
        var obj = { };

        var numObj = new Number(-2);

        numObj.writable = true;

        Object.defineProperty(obj, "property", numObj);

        var beforeWrite = obj.hasOwnProperty("property");

        obj.property = "isWritable";

        var afterWrite = (obj.property === "isWritable");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
