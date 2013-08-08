/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-195.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property, 'P' property doesn't exist in 'O', test 'P' is defined as data property when 'desc' is generic descriptor  (15.4.5.1 step 4.c)
 */


function testcase() {
        var arr = [];

        Object.defineProperties(arr, {
            "0": {
                enumerable: true
            }
        });

        return dataPropertyAttributesAreCorrect(arr, "0", undefined, false, true, false);
    }
runTestCase(testcase);
