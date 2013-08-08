/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-67.js
 * @description Object.defineProperty - both desc.value and name.value are two strings which have same length and same characters in corresponding positions (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { value: "abcd" });

        Object.defineProperty(obj, "foo", { value: "abcd" });
        return dataPropertyAttributesAreCorrect(obj, "foo", "abcd", false, false, false);
    }
runTestCase(testcase);
