/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.4/15.2.4.2/15.2.4.2-1-1.js
 * @description Object.prototype.toString - '[object Undefined]' will be returned when 'this' value is undefined
 */


function testcase() {
        return Object.prototype.toString.call(undefined) === "[object Undefined]";
    }
runTestCase(testcase);
