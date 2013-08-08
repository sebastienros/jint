/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-241.js
 * @description Object.create - 'get' property of one property in 'Properties' is own accessor property without a get function (8.10.5 step 7.a)
 */


function testcase() {
        var descObj = {};

        Object.defineProperty(descObj, "get", {
            set: function () { }
        });

        var newObj = Object.create({}, {
            prop: descObj 
        });

        return newObj.hasOwnProperty("prop") && typeof (newObj.prop) === "undefined";
    }
runTestCase(testcase);
