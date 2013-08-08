/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-6.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a number (value is a positive number)
 */


function testcase() {

        var obj = { 99: true, 100: 100, length: 100 };

        return Array.prototype.lastIndexOf.call(obj, true) === 99 &&
            Array.prototype.lastIndexOf.call(obj, 100) === -1;
    }
runTestCase(testcase);
