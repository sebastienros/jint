/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-186.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index named property, 'name' is boundary value 2^32 + 1 (15.4.5.1 step 4.a)
 */


function testcase() {
        var arrObj = [];

        Object.defineProperty(arrObj, 4294967297, {
            value: 100
        });

        return arrObj.hasOwnProperty("4294967297") && arrObj.length === 0 && arrObj[4294967297] === 100;
    }
runTestCase(testcase);
