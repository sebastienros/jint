/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-174.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is the length property of 'O', the [[Value]] field of 'desc' is less than value of  the length property, test the configurable large index named property of 'O' can be deleted (15.4.5.1 step 3.l.ii)
 */


function testcase() {

        var arr = [0, 1];

        Object.defineProperties(arr, {
            length: {
                value: 1
            }
        });

        return !arr.hasOwnProperty("1");
    }
runTestCase(testcase);
