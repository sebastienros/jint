/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-258.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index named property that already exists on 'O' is accessor property and 'desc' is accessor descriptor, test setting the [[Set]] attribute value of 'P' as undefined  (15.4.5.1 step 4.c)
 */


function testcase() {

        var arr = [];

        Object.defineProperty(arr, "0", {
            set: function () { },
            configurable: true
        });

        Object.defineProperties(arr, {
            "0": {
                set: undefined
            }
        });
        return accessorPropertyAttributesAreCorrect(arr, "0", undefined, undefined, undefined, false, true);
    }
runTestCase(testcase);
