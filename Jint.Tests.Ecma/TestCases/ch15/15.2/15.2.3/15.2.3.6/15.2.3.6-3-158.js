/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-158.js
 * @description Object.defineProperty - 'writable' property in 'Attributes' is own accessor property  (8.10.5 step 6.a)
 */


function testcase() {
        var obj = { };

        var attr = { };
        Object.defineProperty(attr, "writable", {
            get: function () {
                return true;
            }
        });

        Object.defineProperty(obj, "property", attr);

        var beforeWrite = obj.hasOwnProperty("property");

        obj.property = "isWritable";

        var afterWrite = (obj.property === "isWritable");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
