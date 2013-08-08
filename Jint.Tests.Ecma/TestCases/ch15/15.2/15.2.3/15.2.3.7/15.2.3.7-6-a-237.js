/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-237.js
 * @description Object.defineProperties - TypeError is thrown if 'O' is an Array, 'P' is an array index named property that already exists on 'O' is data property with  [[Configurable]], [[Writable]] false, 'desc' is data descriptor, [[Value]] field of 'desc' and the [[Value]] attribute value of 'P' are two numbers with different vaule (15.4.5.1 step 4.c)
 */


function testcase() {
        var arr = [];

        Object.defineProperty(arr, "1", {
            value: 12
        });

        try {
            Object.defineProperties(arr, {
                "1": {
                    value: 36
                }
            });
            return false;
        } catch (ex) {
            return (ex instanceof TypeError) && dataPropertyAttributesAreCorrect(arr, "1", 12, false, false, false);
        }
    }
runTestCase(testcase);
