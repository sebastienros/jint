/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-156.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is the length property of 'O', test the [[Value]] field of 'desc' which equals to value of the length property is defined into 'O' without deleting any property with large index named (15.4.5.1 step 3.f)
 */


function testcase() {

        var arr = [0, , 2];
        try {
            Object.defineProperties(arr, {
                length: {
                    value: 3
                }
            });

            return arr.length === 3 && arr[0] === 0 && !arr.hasOwnProperty("1") && arr[2] === 2;
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
