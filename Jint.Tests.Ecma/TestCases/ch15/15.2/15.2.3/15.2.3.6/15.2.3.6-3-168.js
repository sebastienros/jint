/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-168.js
 * @description Object.defineProperty - 'Attributes' is a Boolean object that uses Object's [[Get]] method to access the 'writable' property  (8.10.5 step 6.a)
 */


function testcase() {
        var obj = { };

        var boolObj = new Boolean(true);

        boolObj.writable = true;

        Object.defineProperty(obj, "property", boolObj);

        var beforeWrite = obj.hasOwnProperty("property");

        obj.property = "isWritable";

        var afterWrite = (obj.property === "isWritable");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
