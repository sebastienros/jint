/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-234.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index property, 'P' is data property and 'desc' is data descriptor, and the [[Configurable]] attribute value of 'P' is false, test TypeError is thrown if the [[Writable]] attribute value of 'P' is false, and the type of the [[Value]] field of 'desc' is different from the type of the [[Value]] attribute value of 'P'  (15.4.5.1 step 4.c)
 */


function testcase() {

        var arr = [];

        Object.defineProperty(arr, "1", {
            value: 3,
            configurable: false,
            writable: false
        });

        try {

            Object.defineProperties(arr, {
                "1": {
                    value: "abc"
                }
            });
            return false;
        } catch (ex) {
            return (ex instanceof TypeError) && dataPropertyAttributesAreCorrect(arr, "1", 3, false, false, false);
        }
    }
runTestCase(testcase);
