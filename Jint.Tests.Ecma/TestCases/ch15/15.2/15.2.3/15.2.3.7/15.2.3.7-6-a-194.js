/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-194.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property, 'P' property doesn't exist in 'O', test TypeError is thrown when 'O' is not extensible  (15.4.5.1 step 4.c)
 */


function testcase() {
        var arr = [];
        Object.preventExtensions(arr);

        try {
            Object.defineProperties(arr, {
                "0": {
                    value: 1
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && (arr.hasOwnProperty("0") === false);
        }
    }
runTestCase(testcase);
