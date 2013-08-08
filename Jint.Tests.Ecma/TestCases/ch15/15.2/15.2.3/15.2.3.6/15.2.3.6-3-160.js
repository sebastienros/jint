/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-160.js
 * @description Object.defineProperty - 'writable' property in 'Attributes' is own accessor property that overrides an inherited data property  (8.10.5 step 6.a)
 */


function testcase() {
        var obj = {};

        var proto = {
            writable: false
        };

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();
        Object.defineProperty(child, "writable", {
            get: function () {
                return true;
            }
        });

        Object.defineProperty(obj, "property", child);

        var beforeWrite = obj.hasOwnProperty("property");

        obj.property = "isWritable";

        var afterWrite = (obj.property === "isWritable");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
