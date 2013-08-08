/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-250.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property that already exists on 'O' is data property and 'desc' is data descriptor, test updating the [[Writable]] attribute value of 'P'  (15.4.5.1 step 4.c)
 */


function testcase() {

        var arr = [100];

        try {
            Object.defineProperties(arr, {
                "0": {
                    writable: false
                }
            });
            return dataPropertyAttributesAreCorrect(arr, "0", 100, false, true, true);
        } catch (ex) {
            return false;
        }
    }
runTestCase(testcase);
