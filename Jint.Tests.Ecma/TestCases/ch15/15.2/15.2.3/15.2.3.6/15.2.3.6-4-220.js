/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-220.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index property, the [[Value]] field of 'desc' and the [[Value]] attribute value of 'name' are two numbers with same vaule (15.4.5.1 step 4.c)
 */


function testcase() {
        var arrObj = [];

        Object.defineProperty(arrObj, "0", { value: 101 });

        Object.defineProperty(arrObj, "0", { value: 101 });
        return dataPropertyAttributesAreCorrect(arrObj, "0", 101, false, false, false);
    }
runTestCase(testcase);
