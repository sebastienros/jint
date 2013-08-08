/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-229.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index property, the [[Writable]] field of 'desc' and the [[Writable]] attribute value of 'name' are two booleans with different values (15.4.5.1 step 4.c)
 */


function testcase() {
        var arrObj = [];

        Object.defineProperty(arrObj, "0", { writable: false, configurable: true });

        Object.defineProperty(arrObj, "0", { writable: true });
        return dataPropertyAttributesAreCorrect(arrObj, "0", undefined, true, false, true);
    }
runTestCase(testcase);
