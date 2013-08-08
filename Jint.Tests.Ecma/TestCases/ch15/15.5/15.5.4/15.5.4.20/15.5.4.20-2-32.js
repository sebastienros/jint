/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-32.js
 * @description String.prototype.trim - argument 'this' is a string(value is '123#$%abc')
 */


function testcase() {
        return String.prototype.trim.call("123#$%abc") === "123#$%abc";
    }
runTestCase(testcase);
