/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-65.js
 * @description Object.defineProperty - desc.value = -0 and name.value = +0 (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { value: +0 });

        try {
            Object.defineProperty(obj, "foo", { value: -0 });
            return false;
        } catch (e) {
            return e instanceof TypeError && dataPropertyAttributesAreCorrect(obj, "foo", +0, false, false, false);
        }
    }
runTestCase(testcase);
