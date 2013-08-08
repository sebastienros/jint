/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-29.js
 * @description Object.defineProperties - 'P' doesn't exist in 'O', test [[Enumerable]] of 'P' is set as false value if absent in data descriptor 'desc' (8.12.9 step 4.a.i)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            prop: { value: 1001}
        });

        for (var prop in obj) {
            if (obj.hasOwnProperty(prop)) {
                if (prop === "prop") {
                    return false;
                }
            }
        }
        return true;
    }
runTestCase(testcase);
