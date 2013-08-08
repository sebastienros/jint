/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-1.js
 * @description String.prototype.trim - 'S' is a string with all LineTerminator
 */


function testcase() {

        var lineTerminatorsStr = "\u000A\u000D\u2028\u2029";
        return (lineTerminatorsStr.trim() === "");
    }
runTestCase(testcase);
