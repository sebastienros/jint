/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-148.js
 * @description Object.defineProperties - 'writable' property of 'descObj' is own accessor property without a get function (8.10.5 step 6.a)
 */


function testcase() {
        var obj = {};

        var descObj = {};

        Object.defineProperty(descObj, "writable", {
            set: function () { }
        });

        Object.defineProperties(obj, {
            property: descObj
        });

        obj.property = "isWritable";

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
