/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-9.js
 * @description String.prototype.trim works for a String object which value is undefined
 */


function testcase() {
        var strObj = new String(undefined);
        return strObj.trim() === "undefined";
    }
runTestCase(testcase);
