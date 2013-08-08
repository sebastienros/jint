/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-17.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is a string containing -Infinity
 */


function testcase() {

        return [true].lastIndexOf(true, "-Infinity") === -1;
    }
runTestCase(testcase);
