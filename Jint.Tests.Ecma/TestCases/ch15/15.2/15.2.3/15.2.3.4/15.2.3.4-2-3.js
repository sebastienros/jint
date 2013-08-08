/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-2-3.js
 * @description Object.getOwnPropertyNames - length of returned array is initialized to 0
 */


function testcase() {

        var obj = {};
        var result = Object.getOwnPropertyNames(obj);

        return result.length === 0;
    }
runTestCase(testcase);
