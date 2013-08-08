/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-7.js
 * @description Array.prototype.lastIndexOf applied to string primitive
 */


function testcase() {

        return Array.prototype.lastIndexOf.call("abc", "c") === 2;
    }
runTestCase(testcase);
