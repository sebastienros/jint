/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-183.js
 * @description Object.defineProperties - TypeError is not thrown if 'O' is an Array, 'P' is an array index named property, [[Writable]] attribute of the length property in 'O' is false, value of 'P' is less than value of the length property in'O'  (15.4.5.1 step 4.b)
 */


function testcase() {
        var arr = [1, 2, 3];

        Object.defineProperty(arr, "length", {
            writable: false
        });

        Object.defineProperties(arr, {
            "1": {
                value: "abc"
            }
        });

        return arr[0] === 1 && arr[1] === "abc" && arr[2] === 3;
    }
runTestCase(testcase);
