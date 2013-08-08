/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-39.js
 * @description Object.defineProperty - 'O' is a Date object that uses Object's [[GetOwnProperty]] method to access the 'name' property (8.12.9 step 1)
 */


function testcase() {
        var desc = new Date();

        Object.defineProperty(desc, "foo", {
            value: 12,
            configurable: false
        });

        try {
            Object.defineProperty(desc, "foo", {
                value: 11,
                configurable: true
            });
            return false;
        } catch (e) {
            return e instanceof TypeError && desc.foo === 12;
        }
    }
runTestCase(testcase);
