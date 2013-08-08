/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-4.js
 * @description Object.getOwnPropertyDescriptor - 'P' is own data property that overrides an inherited accessor property
 */


function testcase() {

        var proto = {};
        Object.defineProperty(proto, "property", {
            get: function () {
                return "inheritedDataProperty";
            },
            configurable: true
        });

        var Con = function () { };
        Con.ptototype = proto;

        var child = new Con();
        Object.defineProperty(child, "property", {
            value: "ownDataProperty",
            configurable: true
        });

        var desc = Object.getOwnPropertyDescriptor(child, "property");

        return desc.value === "ownDataProperty";
    }
runTestCase(testcase);
