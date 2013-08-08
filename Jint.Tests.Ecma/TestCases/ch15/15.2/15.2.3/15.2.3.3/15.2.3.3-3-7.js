/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-7.js
 * @description Object.getOwnPropertyDescriptor - 'P' is own accessor property that overrides an inherited data property
 */


function testcase() {

        var proto = {
            property: "inheritedDataProperty"
        };

        var Con = function () { };
        Con.ptototype = proto;

        var child = new Con();
        var fun = function () {
            return "ownAccessorProperty";
        };
        Object.defineProperty(child, "property", {
            get: fun,
            configurable: true
        });

        var desc = Object.getOwnPropertyDescriptor(child, "property");

        return desc.get === fun;
    }
runTestCase(testcase);
