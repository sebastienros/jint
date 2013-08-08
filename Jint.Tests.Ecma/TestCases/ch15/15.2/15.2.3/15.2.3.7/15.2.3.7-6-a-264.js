/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-264.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property, test the length property of 'O' is set as ToUint32('P') + 1 if ToUint32('P') equals to value of the length property in 'O' (15.4.5.1 step 4.e.ii)
 */


function testcase() {

        var arr = [];

        arr.length = 3; // default value of length: writable: true, configurable: false, enumerable: false

        Object.defineProperties(arr, {
            "3": {
                value: 26
            }
        });
        return arr.length === 4 && arr[3] === 26;
    }
runTestCase(testcase);
