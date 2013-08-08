/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-3.js
 * @description Object.getOwnPropertyDescriptor - 'P' is own data property that overrides an inherited data property
 */


function testcase() {

        var proto = {
            property: "inheritedDataProperty"
        };

        var Con = function () { };
        Con.ptototype = proto;

        var child = new Con();
        child.property = "ownDataProperty";

        var desc = Object.getOwnPropertyDescriptor(child, "property");

        return desc.value === "ownDataProperty";
    }
runTestCase(testcase);
