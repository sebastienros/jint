/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-8.js
 * @description String.prototype.trim works for a primitive string (value is '    abc') 
 */


function testcase() {
        var strObj = String("    abc");
        return "abc" === strObj.trim() && strObj.toString() === "    abc";
    }
runTestCase(testcase);
