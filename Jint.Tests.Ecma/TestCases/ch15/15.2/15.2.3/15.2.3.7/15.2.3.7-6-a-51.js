/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-51.js
 * @description Object.defineProperties - both desc.value and P.value are boolean values with the same value (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        var desc = { value: true };
        Object.defineProperty(obj, "foo", desc);

        Object.defineProperties(obj, {
            foo: {
                value: true
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", true, false, false, false);
    }
runTestCase(testcase);
