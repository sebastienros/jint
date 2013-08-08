/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-2.js
 * @description String.prototype.trim - argument 'this' is a boolean whose value is true
 */


function testcase() {
        return String.prototype.trim.call(true) === "true";
    }
runTestCase(testcase);
