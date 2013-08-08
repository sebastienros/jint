/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-2.js
 * @description Object.getOwnPropertyDescriptor - 'P' is inherited data property
 */


function testcase() {

        var proto = {
            property: "inheritedDataProperty"
        };

        var Con = function () { };
        Con.ptototype = proto;

        var child = new Con();

        var desc = Object.getOwnPropertyDescriptor(child, "property");

        return typeof desc === "undefined";
    }
runTestCase(testcase);
