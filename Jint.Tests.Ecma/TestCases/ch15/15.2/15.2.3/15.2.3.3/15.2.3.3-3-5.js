/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-5.js
 * @description Object.getOwnPropertyDescriptor - 'P' is own accessor property
 */


function testcase() {

        var obj = {};
        var fun = function () {
            return "ownAccessorProperty";
        };
        Object.defineProperty(obj, "property", {
            get: fun,
            configurable: true
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "property");

        return desc.get === fun;
    }
runTestCase(testcase);
