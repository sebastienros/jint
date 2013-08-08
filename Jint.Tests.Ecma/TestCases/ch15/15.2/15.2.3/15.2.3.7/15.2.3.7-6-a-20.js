/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-20.js
 * @description Object.defineProperties - 'O' is a JSON object which implements its own [[GetOwnProperty]] method to get 'P' (8.12.9 step 1 ) 
 */


function testcase() {

        try {
            Object.defineProperty(JSON, "prop", {
                value: 11,
                writable: true,
                configurable: true
            });
            var hasProperty = JSON.hasOwnProperty("prop") && JSON.prop === 11;
            Object.defineProperties(JSON, {
                prop: {
                    value: 12
                }
            });
            return hasProperty && JSON.prop === 12;
        } finally {
            delete JSON.prop;
        }
    }
runTestCase(testcase);
