/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-13.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a string containing a decimal number
 */


function testcase() {

        var obj = { 4: 4, 5: 5, length: "5.512345" };

        return Array.prototype.lastIndexOf.call(obj, 4) === 4 &&
            Array.prototype.lastIndexOf.call(obj, 5) === -1;
    }
runTestCase(testcase);
