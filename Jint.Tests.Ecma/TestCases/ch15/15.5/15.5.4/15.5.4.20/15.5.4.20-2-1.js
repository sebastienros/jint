/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-1.js
 * @description String.prototype.trim - argument 'this' is a boolean whose value is false
 */


function testcase() {
        return String.prototype.trim.call(false) === "false";
    }
runTestCase(testcase);
