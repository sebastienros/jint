/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-12.js
 * @description String.prototype.trim - argument 'this' is a number that converts to a string (value is 1(following 20 zeros))
 */


function testcase() {
        return String.prototype.trim.call(100000000000000000000) === "100000000000000000000";
    }
runTestCase(testcase);
