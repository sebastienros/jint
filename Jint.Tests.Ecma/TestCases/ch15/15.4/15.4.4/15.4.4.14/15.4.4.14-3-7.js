/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-7.js
 * @description Array.prototype.indexOf - value of 'length' is a number (value is negative)
 */


function testcase() {

        var obj = { 4: true, 5: false, length: 5 - Math.pow(2, 32) };

        return Array.prototype.indexOf.call(obj, true) === 4 &&
            Array.prototype.indexOf.call(obj, false) === -1;
    }
runTestCase(testcase);
