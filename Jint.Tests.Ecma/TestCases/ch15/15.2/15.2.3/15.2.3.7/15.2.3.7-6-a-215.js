/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-215.js
 * @description Object.defineProperties - 'O' is an Array, 'name' is an array index property, the [[Value]] field of 'desc' and the [[Value]] attribute value of 'name' are two numbers with same vaule  (15.4.5.1 step 4.c)
 */


function testcase() {
        var arr = [];

        Object.defineProperty(arr, "0", {
            value: 101
        });

        try {
            Object.defineProperties(arr, {
                "0": {
                    value: 101
                }
            });
            return dataPropertyAttributesAreCorrect(arr, "0", 101, false, false, false);
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
