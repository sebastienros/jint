/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-266.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is generic property that won't exist on 'O', and 'desc' is data descriptor, test 'P' is defined in 'O' with all correct attribute values (15.4.5.1 step 5)
 */


function testcase() {

        var arr = [];

        Object.defineProperties(arr, {
            "property": {
                value: 12,
                writable: true,
                enumerable: true,
                configurable: true
            }
        });
        return dataPropertyAttributesAreCorrect(arr, "property", 12, true, true, true) && arr.length === 0;
    }
runTestCase(testcase);
