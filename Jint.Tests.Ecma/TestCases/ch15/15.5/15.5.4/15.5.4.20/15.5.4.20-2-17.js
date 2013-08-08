/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-17.js
 * @description String.prototype.trim - argument 'this' is a number that converts to string (value is 1e+22)
 */


function testcase() {
        return String.prototype.trim.call(1e+22) === "1e+22";
    }
runTestCase(testcase);
