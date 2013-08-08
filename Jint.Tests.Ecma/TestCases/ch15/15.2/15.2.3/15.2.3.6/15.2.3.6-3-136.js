/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-136.js
 * @description Object.defineProperty - 'value' property in 'Attributes' is own accessor property without a get function  (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};

        var attr = {};
        Object.defineProperty(attr, "value", {
            set: function () { }
        });

        Object.defineProperty(obj, "property", attr);

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
