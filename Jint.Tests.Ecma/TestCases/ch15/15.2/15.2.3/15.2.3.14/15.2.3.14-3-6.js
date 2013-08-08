/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-6.js
 * @description Object.keys - returns the standard built-in Array (instanceof Array)
 */


function testcase() {
        var obj = {};

        var arr = Object.keys(obj);

        return arr instanceof Array;
    }
runTestCase(testcase);
