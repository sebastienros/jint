/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-248.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property that already exists on 'O' is data property and 'desc' is data descriptor, test setting the [[Value]] attribute value of 'P' as undefined  (15.4.5.1 step 4.c)
 */


function testcase() {

        var arr = [12];

        try {
            Object.defineProperties(arr, {
                "0": {
                    value: undefined
                }
            });
            return dataPropertyAttributesAreCorrect(arr, "0", undefined, true, true, true);
        } catch (ex) {
            return false;
        }
    }
runTestCase(testcase);
