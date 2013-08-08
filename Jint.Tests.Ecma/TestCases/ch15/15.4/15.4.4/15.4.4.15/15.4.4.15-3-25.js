/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-25.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a negative non-integer, ensure truncation occurs in the proper direction
 */


function testcase() {

        var obj = { 1: true, 2: false, length: -4294967294.5 };

        return Array.prototype.lastIndexOf.call(obj, true) === 1 &&
            Array.prototype.lastIndexOf.call(obj, false) === -1;
    }
runTestCase(testcase);
