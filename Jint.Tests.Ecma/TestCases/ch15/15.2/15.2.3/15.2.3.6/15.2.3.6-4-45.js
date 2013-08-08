/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-45.js
 * @description Object.defineProperty - 'O' is the global object that uses Object's [[GetOwnProperty]] method to access the 'name' property (8.12.9 step 1)
 */


function testcase() {
        try {
            Object.defineProperty(fnGlobalObject(), "foo", {
                value: 12,
                configurable: true
            });

            return dataPropertyAttributesAreCorrect(fnGlobalObject(), "foo", 12, false, false, true);
        } finally {
            delete fnGlobalObject().foo;
        }
    }
runTestCase(testcase);
