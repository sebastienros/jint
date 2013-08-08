/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-24.js
 * @description Object.defineProperties - 'O' is the global object which implements its own [[GetOwnProperty]] method to get 'P' (8.12.9 step 1 ) 
 */


function testcase() {

        try {
            Object.defineProperty(fnGlobalObject(), "prop", {
                value: 11,
                writable: true,
                enumerable: true,
                configurable: true
            });

            Object.defineProperties(fnGlobalObject(), {
                prop: {
                    value: 12
                }
            });
            return dataPropertyAttributesAreCorrect(fnGlobalObject(), "prop", 12, true, true, true);
        }  finally {
            delete fnGlobalObject().prop;
        }
    }
runTestCase(testcase);
