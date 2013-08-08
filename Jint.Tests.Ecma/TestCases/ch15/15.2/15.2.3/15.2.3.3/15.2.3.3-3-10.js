/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-3-10.js
 * @description Object.getOwnPropertyDescriptor - 'P' is not an existing property
 */


function testcase() {

        var obj = {
            property: "ownDataProperty"
        };

        var desc = Object.getOwnPropertyDescriptor(obj, "propertyNonExist");

        return typeof desc === "undefined";
    }
runTestCase(testcase);
