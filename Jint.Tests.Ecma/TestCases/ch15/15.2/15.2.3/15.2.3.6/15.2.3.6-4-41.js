/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-41.js
 * @description Object.defineProperty - 'O' is the JSON object that uses Object's [[GetOwnProperty]] method to access the 'name' property (8.12.9 step 1)
 */


function testcase() {

        try {
            Object.defineProperty(JSON, "foo", {
                value: 12,
                configurable: true
            });

            return dataPropertyAttributesAreCorrect(JSON, "foo", 12, false, false, true);
        } finally {
            delete JSON.foo;
        }
    }
runTestCase(testcase);
