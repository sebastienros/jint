/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-177.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is the length property of 'O', the [[Value]] field of 'desc' is less than value of  the length property, test the [[Writable]] attribute of the length property is set to false at last when the [[Writable]] field of 'desc' is false and 'O' doesn't contain non-configurable large index named property (15.4.5.1 step 3.m)
 */


function testcase() {
    
        var arr = [0, 1];

        try {
            Object.defineProperties(arr, {
                length: {
                    value: 0,
                    writable: false
                }
            });

            arr.length = 10; //try to overwrite length value of arr
            return !arr.hasOwnProperty("1") && arr.length === 0 && !arr.hasOwnProperty("0");
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
