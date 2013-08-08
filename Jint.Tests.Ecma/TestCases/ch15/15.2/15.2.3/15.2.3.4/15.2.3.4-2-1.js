/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-2-1.js
 * @description Object.getOwnPropertyNames - returned array is an array according to Array.isArray
 */


function testcase() {

        var obj = {};
        var result = Object.getOwnPropertyNames(obj);

        return Array.isArray(result);
    }
runTestCase(testcase);
