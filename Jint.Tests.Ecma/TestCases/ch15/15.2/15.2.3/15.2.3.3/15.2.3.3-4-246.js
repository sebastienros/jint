/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-246.js
 * @description Object.getOwnPropertyDescriptor - ensure that 'set' property of returned object is data property with correct 'configurable' attribute
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

        var propDefined = "set" in desc;

        try {
            delete desc.set;
            var propDeleted = "set" in desc;

            return propDefined && !propDeleted;
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
