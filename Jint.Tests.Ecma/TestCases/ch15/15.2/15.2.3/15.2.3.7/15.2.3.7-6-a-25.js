/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-25.js
 * @description Object.defineProperties - 'P' doesn't exist in 'O', test TypeError is thrown when 'O' is not extensible (8.12.9 step 3)
 */


function testcase() {
        var obj = {};
        Object.preventExtensions(obj);

        try {
            Object.defineProperties(obj, {
                prop: {
                    value: 12,
                    configurable: true
                }
            });
            return false;
        } catch (e) {
            return e instanceof TypeError && !obj.hasOwnProperty("prop");
        }
    }
runTestCase(testcase);
