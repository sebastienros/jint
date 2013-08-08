/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-49.js
 * @description Object.defineProperty - 'name' property doesn't exist in 'O', test [[Enumerable]] of 'name' property of 'Attributes' is set as false value if absent in data descriptor 'desc' (8.12.9 step 4.a.i)
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "property", {
            value: 1001,
            writable: true,
            configurable: true
        });
        return dataPropertyAttributesAreCorrect(obj, "property", 1001, true, false, true);
    }
runTestCase(testcase);
