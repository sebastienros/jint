/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-3-151.js
 * @description Object.defineProperty - 'Attributes' is the global object that uses Object's [[Get]] method to access the 'value' property  (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};

        try {
            fnGlobalObject().value = "global";

            Object.defineProperty(obj, "property", fnGlobalObject());

            return obj.property === "global";
        } finally {
            delete fnGlobalObject().value;
        }
    }
runTestCase(testcase);
