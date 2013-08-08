/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-6.js
 * @description Object.getOwnPropertyDescriptor - 'P' is inherited accessor property
 */


function testcase() {

        var proto = {};
        var fun = function () {
            return "ownAccessorProperty";
        };
        Object.defineProperty(proto, "property", {
            get: fun,
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        var desc = Object.getOwnPropertyDescriptor(child, "property");

        return typeof desc === "undefined";
    }
runTestCase(testcase);
