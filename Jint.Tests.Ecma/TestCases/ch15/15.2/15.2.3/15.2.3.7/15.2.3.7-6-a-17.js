/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-17.js
 * @description Object.defineProperties - 'O' is the Math object which implements its own [[GetOwnProperty]] method to get 'P' (8.12.9 step 1 ) 
 */


function testcase() {

        try {
            Object.defineProperty(Math, "prop", {
                value: 11,
                writable: true,
                configurable: true
            });
            var hasProperty = Math.hasOwnProperty("prop") && Math.prop === 11;

            Object.defineProperties(Math, {
                prop: {
                    value: 12
                }
            });
            return hasProperty && Math.prop === 12;
        } finally {
            delete Math.prop;
        }
    }
runTestCase(testcase);
