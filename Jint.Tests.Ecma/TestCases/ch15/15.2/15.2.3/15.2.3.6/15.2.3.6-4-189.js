/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-189.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index named property, test TypeError is thrown if the [[Writable]] attribute of the length property in 'O' is false and value of 'name' is greater than value of the length property (15.4.5.1 step 4.b)
 */


function testcase() {
        var arrObj = [1, 2, 3];

        Object.defineProperty(arrObj, "length", {
            writable: false
        });

        try {
            Object.defineProperty(arrObj, 4, {
                value: "abc"
            });

            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);
