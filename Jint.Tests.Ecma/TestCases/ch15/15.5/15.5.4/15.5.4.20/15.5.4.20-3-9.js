/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-9.js
 * @description String.prototype.trim - 'S' is a string with null character ('\0')
 */


function testcase() {
            return "\0".trim() === "\0";
    }
runTestCase(testcase);
