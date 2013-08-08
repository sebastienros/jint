/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-8.js
 * @description Array.prototype.indexOf - value of 'fromIndex' is a number (value is +0)
 */


function testcase() {

        return [true].indexOf(true, +0) === 0;
    }
runTestCase(testcase);
