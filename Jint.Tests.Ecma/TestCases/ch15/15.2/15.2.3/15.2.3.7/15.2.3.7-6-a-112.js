/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-112.js
 * @description Object.defineProperties - 'O' is an Array, test the length property of 'O' is own data property (15.4.5.1 step 1)
 */


function testcase() {

        var arr = [0, 1];
        Object.defineProperty(arr, "1", {
            value: 1,
            configurable: false
        });
        try {

            Object.defineProperties(arr, {
                length: { value: 1 }
            });
            return false;
        } catch (ex) {
            var desc = Object.getOwnPropertyDescriptor(arr, "length");

            return ex instanceof TypeError && desc.value === 2 &&
                desc.writable && !desc.enumerable && !desc.configurable;
        }
    }
runTestCase(testcase);
