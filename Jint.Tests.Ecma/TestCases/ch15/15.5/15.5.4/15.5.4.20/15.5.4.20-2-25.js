/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-25.js
 * @description String.prototype.trim - argument 'this' is a decimal that converts to a string (value is 123.456)
 */


function testcase() {
        return String.prototype.trim.call(123.456) === "123.456";
    }
runTestCase(testcase);
