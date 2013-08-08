/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-162.js
 * @description Object.defineProperty - 'writable' property in 'Attributes' is own accessor property without a get function  (8.10.5 step 6.a)
 */


function testcase() {
        var obj = {};

        var attr = {};
        Object.defineProperty(attr, "writable", {
            set: function () { }
        });

        Object.defineProperty(obj, "property", attr);

        var beforeWrite = obj.hasOwnProperty("property");

        obj.property = "isWritable";

        var afterWrite = (typeof (obj.property) === "undefined");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
