/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-13.js
 * @description String.prototype.trim - 'S' is a string that starts with null character and ends with null character
 */


function testcase() {
        return "\0\u0000abc\0\u0000".trim() === "\0\u0000abc\0\u0000";
    }
runTestCase(testcase);
