/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.4/15.2.4.2/15.2.4.2-2-2.js
 * @description Object.prototype.toString - '[object Null]' will be returned when 'this' value is null
 */


function testcase() {
        return Object.prototype.toString.apply(null, []) === "[object Null]";
    }
runTestCase(testcase);
