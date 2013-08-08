/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-9.js
 * @description Function.prototype.bind - F can get own accessor property without a get function
 */


function testcase() {

        var foo = function () { };

        var obj = foo.bind({});
        Object.defineProperty(obj, "property", {
            set: function () {}
        });
        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
