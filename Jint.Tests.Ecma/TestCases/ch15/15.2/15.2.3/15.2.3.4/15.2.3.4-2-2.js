/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-2-2.js
 * @description Object.getOwnPropertyNames - returned array is an instance of Array
 */


function testcase() {
        var obj = {};
        var result = Object.getOwnPropertyNames(obj);

        return result instanceof Array;
    }
runTestCase(testcase);
