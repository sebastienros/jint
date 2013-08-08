/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-213.js
 * @description Object.defineProperties - 'O' is an Array, 'name' is an array index property, the [[Value]] field of 'desc' is +0, and the [[Value]] attribute value of 'name' is -0  (15.4.5.1 step 4.c)
 */


function testcase() {
        var arr = [];

        Object.defineProperty(arr, "0", {
            value: -0
        });

        try {
            Object.defineProperties(arr, {
                "0": {
                    value: +0
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && dataPropertyAttributesAreCorrect(arr, "0", -0, false, false, false);
        }
    }
runTestCase(testcase);
