/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-5.js
 * @description Date.prototype.toISOString - The returned string is the UTC time zone(0)
 */


function testcase() {
        var dateStr = (new Date()).toISOString();
        return dateStr[dateStr.length - 1] === "Z";
    }
runTestCase(testcase);
