/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-10.js
 * @description String.prototype.trim - 'S' is a string with null character ('\u0000')
 */


function testcase() {
        return "\u0000".trim() === "\u0000";
    }
runTestCase(testcase);
