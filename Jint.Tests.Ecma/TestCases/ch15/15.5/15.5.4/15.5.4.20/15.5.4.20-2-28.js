/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-28.js
 * @description String.prototype.trim - argument 'this' is an empty string 
 */


function testcase() {
        return String.prototype.trim.call("") === "";
    }
runTestCase(testcase);
