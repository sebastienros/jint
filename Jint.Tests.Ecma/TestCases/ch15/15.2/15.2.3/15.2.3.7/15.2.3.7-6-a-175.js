/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-175.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is the length property of 'O', the [[Value]] field of 'desc' is less than value of  the length property, test value of the length property is set to the last non-configurable index named property of 'O' plus 1 (15.4.5.1 step 3.l.iii.1)
 */


function testcase() {

        var arr = [0, 1, 2, 3];

        Object.defineProperty(arr, "1", {
            configurable: false
        });

        try {
            Object.defineProperties(arr, {
                length: {
                    value: 1
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && (arr.length === 2);
        }
    }
runTestCase(testcase);
