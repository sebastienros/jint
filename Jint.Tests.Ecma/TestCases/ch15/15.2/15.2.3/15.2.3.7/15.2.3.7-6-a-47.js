/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-47.js
 * @description Object.defineProperties - desc.value and P.value are two numbers with the same value (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        var desc = { value: 101 };
        Object.defineProperty(obj, "foo", desc);

        Object.defineProperties(obj, {
            foo: {
                value: 101
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 101, false, false, false);
    }
runTestCase(testcase);
