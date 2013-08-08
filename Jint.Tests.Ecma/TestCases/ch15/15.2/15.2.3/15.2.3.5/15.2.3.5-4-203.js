/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-203.js
 * @description Object.create - one property in 'Properties' is the global object that uses Object's [[Get]] method to access the 'writable' property (8.10.5 step 6.a)
 */


function testcase() {

        try {
            fnGlobalObject().writable = true;

            var newObj = Object.create({}, {
                prop: fnGlobalObject() 
            });

            var beforeWrite = (newObj.hasOwnProperty("prop") && typeof (newObj.prop) === "undefined");

            newObj.prop = "isWritable";

            var afterWrite = (newObj.prop === "isWritable");

            return beforeWrite === true && afterWrite === true;
        } finally {
            delete fnGlobalObject().writable;
        }
    }
runTestCase(testcase);
