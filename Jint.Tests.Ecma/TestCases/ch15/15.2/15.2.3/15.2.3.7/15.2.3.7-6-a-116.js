/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-116.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is the length property of 'O', the [[Value]] field of 'desc' is absent, test TypeError is thrown when updating the [[Configurable]] attribute of the length property from false to true (15.4.5.1 step 3.a.i)
 */


function testcase() {

        var arr = [];

        try {
            Object.defineProperties(arr, {
                length: { configurable: true }
            });

            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
