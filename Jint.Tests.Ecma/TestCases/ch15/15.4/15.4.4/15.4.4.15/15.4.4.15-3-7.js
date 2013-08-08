/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-7.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a number (value is a negative number)
 */


function testcase() {

        var obj = { 4: -Infinity, 5: Infinity, length: 5 - Math.pow(2, 32) };

        return Array.prototype.lastIndexOf.call(obj, -Infinity) === 4 &&
            Array.prototype.lastIndexOf.call(obj, Infinity) === -1;
    }
runTestCase(testcase);
