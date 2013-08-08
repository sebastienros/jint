/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-277.js
 * @description Object.create - 'set' property of one property in 'Properties' is own accessor property without a get function, which overrides an inherited accessor property (8.10.5 step 8.a)
 */


function testcase() {
        var proto = {};
        Object.defineProperty(proto, "set", {
            get: function () {
                return function () { };
            }
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;
        var child = new ConstructFun();
        Object.defineProperty(child, "set", {
            set: function () { }
        });

        var newObj = Object.create({}, {
            prop: child
        });

        var desc = Object.getOwnPropertyDescriptor(newObj, "prop");

        return newObj.hasOwnProperty("prop") && typeof desc.set === "undefined";
    }
runTestCase(testcase);
