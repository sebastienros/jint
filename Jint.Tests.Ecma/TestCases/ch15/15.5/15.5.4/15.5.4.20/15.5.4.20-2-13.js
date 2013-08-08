/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-13.js
 * @description String.prototype.trim - argument 'this' is a number that converts to a string (value is 1(following 21 zeros))
 */


function testcase() {
        return String.prototype.trim.call(1000000000000000000000) === "1e+21";
    }
runTestCase(testcase);
