/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-190.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index named property, 'name' is own data property, test TypeError is thrown on updating the configurable attribute from false to true (15.4.5.1 step 4.c)
 */


function testcase() {
        var arrObj = [];
        Object.defineProperty(arrObj, 0, {
            value: "ownDataProperty",
            configurable: false
        });

        try {
            Object.defineProperty(arrObj, 0, {
                configurable: true
            });
            return false;
        } catch (e) {
            return e instanceof TypeError &&
                dataPropertyAttributesAreCorrect(arrObj, "0", "ownDataProperty", false, false, false);
        }
    }
runTestCase(testcase);
