/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-173.js
 * @description Object.create - one property in 'Properties' is the JSON object that uses Object's [[Get]] method to access the 'value' property (8.10.5 step 5.a)
 */


function testcase() {

        try {
            JSON.value = "JSONValue";

            var newObj = Object.create({}, {
                prop: JSON
            });

            return newObj.prop === "JSONValue";
        } finally {
            delete JSON.value;
        }
    }
runTestCase(testcase);
