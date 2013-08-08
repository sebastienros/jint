/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-56.js
 * @description Object.defineProperties - desc.writable and P.writable are two boolean values with different values (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        var desc = { writable: false, configurable: true };
        Object.defineProperty(obj, "foo", desc);

        Object.defineProperties(obj, {
            foo: {
                writable: true,
                configurable: true
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", undefined, true, false, true);
    }
runTestCase(testcase);
