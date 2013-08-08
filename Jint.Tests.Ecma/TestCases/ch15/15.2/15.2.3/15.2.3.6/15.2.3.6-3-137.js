/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-137.js
 * @description Object.defineProperty - 'value' property in 'Attributes' is own accessor property(without a get function) that overrides an inherited accessor property  (8.10.5 step 5.a)
 */


function testcase() {
        var obj = { };

        var proto = {};
        Object.defineProperty(proto, "value", {
            get: function () {
                return "inheritedAccessorProperty";
            }
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();
        Object.defineProperty(child, "value", {
            set : function () { }
        });

        Object.defineProperty(obj, "property", child);

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
