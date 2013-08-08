/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-37.js
 * @description String.prototype.trim - 'this' is a Number Object that converts to a string
 */


function testcase() {
        return (String.prototype.trim.call(new Number(123)) === "123");
    }
runTestCase(testcase);
