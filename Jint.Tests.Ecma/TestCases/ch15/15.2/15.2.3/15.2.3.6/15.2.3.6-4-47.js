/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-47.js
 * @description Object.defineProperty - 'name' property doesn't exist in 'O', [[Value]] of 'name' property is set as undefined if it is absent in data descriptor 'desc' (8.12.9 step 4.a.i)
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "property", {
            writable: true,
            enumerable: true,
            configurable: false
        });

        return dataPropertyAttributesAreCorrect(obj, "property", undefined, true, true, false);
    }
runTestCase(testcase);
