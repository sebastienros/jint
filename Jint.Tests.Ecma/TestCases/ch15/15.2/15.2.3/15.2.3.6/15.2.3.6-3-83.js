/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-83.js
 * @description Object.defineProperty - 'configurable' property in 'Attributes' is own accessor property without a get function (8.10.5 step 4.a)
 */


function testcase() {
        var obj = { };

        var attr = {};
        Object.defineProperty(attr, "configurable", {
            set : function () { }
        });

        Object.defineProperty(obj, "property", attr);

        var beforeDeleted = obj.hasOwnProperty("property");

        delete obj.property;

        var afterDeleted = obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";

        return beforeDeleted === true && afterDeleted === true;
    }
runTestCase(testcase);
