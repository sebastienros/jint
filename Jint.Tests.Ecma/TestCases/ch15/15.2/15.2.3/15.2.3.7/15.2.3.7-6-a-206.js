/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-206.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property, 'P' makes no change if every field in 'desc' is absent (name is data property)  (15.4.5.1 step 4.c)
 */


function testcase() {
        var arr = [];

        arr[0] = 101; // default value of attributes: writable: true, configurable: true, enumerable: true

        try {
            Object.defineProperties(arr, {
                "0": {}
            });
            return dataPropertyAttributesAreCorrect(arr, "0", 101, true, true, true);
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
