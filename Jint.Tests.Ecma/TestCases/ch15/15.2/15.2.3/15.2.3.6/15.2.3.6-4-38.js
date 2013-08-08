/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-38.js
 * @description Object.defineProperty - 'O' is the Math object that uses Object's [[GetOwnProperty]] method to access the 'name' property (8.12.9 step 1)
 */


function testcase() {
        try {
            Object.defineProperty(Math, "foo", {
                value: 12,
                configurable: true
            });
        
            return dataPropertyAttributesAreCorrect(Math, "foo", 12, false, false, true);
        } finally {
            delete Math.foo;
        }
    }
runTestCase(testcase);
