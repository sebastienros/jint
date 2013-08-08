/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-121.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is the length property of 'O', the [[Value]] field of 'desc' is absent, test TypeError is thrown when updating the [[Enumerable]] attribute of the length property from false to true (15.4.5.1 step 3.a.i)
 */


function testcase() {

        var arrObj = [];
        try {
            Object.defineProperty(arrObj, "length", {
                enumerable: true
            });
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);
