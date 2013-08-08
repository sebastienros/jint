/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-17.js
 * @description Array.prototype.indexOf - value of 'fromIndex' is a string containing -Infinity
 */


function testcase() {

        return [true].indexOf(true, "-Infinity") === 0;
    }
runTestCase(testcase);
