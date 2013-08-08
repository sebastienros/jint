/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-69.js
 * @description Object.defineProperty - both desc.value and name.value are boolean values with the same value (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { value: true });

        Object.defineProperty(obj, "foo", { value: true });
        return dataPropertyAttributesAreCorrect(obj, "foo", true, false, false, false);
    }
runTestCase(testcase);
