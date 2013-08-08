/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-219.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index property, test TypeError is thrown when the [[Value]] field of 'desc' is -0, and the [[Value]] attribute value of 'name' is +0 (15.4.5.1 step 4.c)
 */


function testcase() {
        var arrObj = [];

        Object.defineProperty(arrObj, "0", { value: +0 });

        try {
            Object.defineProperty(arrObj, "0", { value: -0 });
            return false;
        } catch (e) {
            return e instanceof TypeError && dataPropertyAttributesAreCorrect(arrObj, "0", +0, false, false, false);
        }
    }
runTestCase(testcase);
