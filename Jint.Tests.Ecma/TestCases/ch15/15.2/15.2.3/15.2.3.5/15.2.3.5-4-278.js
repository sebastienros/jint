/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-278.js
 * @description Object.create - 'set' property of one property in 'Properties' is an inherited accessor property without a get function (8.10.5 step 8.a)
 */


function testcase() {
        var proto = {};
        Object.defineProperty(proto, "set", {
            set: function () { }
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;
        var child = new ConstructFun();

        var newObj = Object.create({}, {
            prop: child
        });

        var desc = Object.getOwnPropertyDescriptor(newObj, "prop");

        return newObj.hasOwnProperty("prop") && typeof desc.set === "undefined";
    }
runTestCase(testcase);
