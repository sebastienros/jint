/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-165.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is the length property of 'O', the [[Value]] field of 'desc' is less than value of  the length property, test the length property is decreased by 1 (15.4.5.1 step 3.l.i)
 */


function testcase() {

        var arr = [0, 1, 2];

        Object.defineProperty(arr, "1", {
            configurable: false
        });

        Object.defineProperty(arr, "2", {
            configurable: true
        });

        try {
            Object.defineProperties(arr, {
                length: {
                    value: 1
                }
            });
            return false;
        } catch (e) {
            return e instanceof TypeError && arr.length === 2 &&
                !arr.hasOwnProperty("2") && arr[0] === 0 && arr[1] === 1;
        }
    }
runTestCase(testcase);
