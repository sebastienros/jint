/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-246.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index named property, 'name' is data property and 'desc' is data descriptor, and the [[Configurable]] attribute value of 'name' is false, test TypeError is thrown if the [[Writable]] attribute value of 'name' is false, and the [[Value]] field of 'desc' is +0, and the [[Value]] attribute value of 'name' is -0 (15.4.5.1 step 4.c)
 */


function testcase() {
        var arrObj = [];

        Object.defineProperty(arrObj, "1", {
            value: -0

        });

        try {
            Object.defineProperty(arrObj, "1", {
                value: +0
            });

            return false;
        } catch (e) {
            return e instanceof TypeError && dataPropertyAttributesAreCorrect(arrObj, "1", -0, false, false, false);
        }
    }
runTestCase(testcase);
