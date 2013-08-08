/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-263.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property, test the length property of 'O' is not changed if ToUint32('P') is less than value of the length property in 'O' (15.4.5.1 step 4.e)
 */


function testcase() {

        var arr = [];

        arr.length = 3; // default value of length: writable: true, configurable: false, enumerable: false

        Object.defineProperties(arr, {
            "1": {
                value: 26
            }
        });
        return arr.length === 3 && arr[1] === 26;
    }
runTestCase(testcase);
