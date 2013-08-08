/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-190.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property, 'P' is own accessor property  (15.4.5.1 step 4.c)
 */


function testcase() {
        var arr = [];

        Object.defineProperty(arr, "0", {
            get: function () {
                return 11;
            },
            configurable: false
        });

        try {
            Object.defineProperties(arr, {
                "0": {
                    get: function () {
                        return 12;
                    },
                    configurable: true
                }
            });
            return false;
        } catch (e) {
            return e instanceof TypeError && arr[0] === 11;
        }
    }
runTestCase(testcase);
