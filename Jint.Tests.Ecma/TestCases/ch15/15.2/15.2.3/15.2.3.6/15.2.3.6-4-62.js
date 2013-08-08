/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-62.js
 * @description Object.defineProperty - both desc.value and name.value are null (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { value: null });

        Object.defineProperty(obj, "foo", { value: null });
        return dataPropertyAttributesAreCorrect(obj, "foo", null, false, false, false);
    }
runTestCase(testcase);
