/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-208.js
 * @description Object.defineProperty - 'get' property in 'Attributes' is an inherited data property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};
        var proto = {
            get: function () {
                return "inheritedDataProperty";
            }
        };

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();

        Object.defineProperty(obj, "property", child);

        return obj.property === "inheritedDataProperty";
    }
runTestCase(testcase);
