/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-200.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index named property, 'name' property doesn't exist in 'O', test [[Value]] of 'name' property of 'Attributes' is set as undefined if [[Value]] is absent in data descriptor 'desc' (15.4.5.1 step 4.c)
 */


function testcase() {
        var arrObj = [];

        Object.defineProperty(arrObj, "0", {
            writable: true,
            enumerable: true,
            configurable: false
        });

        return dataPropertyAttributesAreCorrect(arrObj, "0", undefined, true, true, false);
    }
runTestCase(testcase);
