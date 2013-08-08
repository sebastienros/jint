/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-244.js
 * @description Object.getOwnPropertyDescriptor - ensure that 'set' property of returned object is data property with correct 'writable' attribute
 */


function testcase() {
        var obj = {};
        var fun = function () {
            return "ownSetProperty";
        };
        Object.defineProperty(obj, "property", {
            set: fun,
            configurable: true
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "property");

        try {
            desc.set = "overwriteSetProperty";
            return desc.set === "overwriteSetProperty";
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
