/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-199.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index named property, 'name' property doesn't exist in 'O', test 'name' is defined as data property when 'desc' is generic descriptor (15.4.5.1 step 4.c)
 */


function testcase() {
        var arrObj = [];

        Object.defineProperty(arrObj, "0", {
            enumerable: true
        });

        return dataPropertyAttributesAreCorrect(arrObj, "0", undefined, false, true, false);
    }
runTestCase(testcase);
