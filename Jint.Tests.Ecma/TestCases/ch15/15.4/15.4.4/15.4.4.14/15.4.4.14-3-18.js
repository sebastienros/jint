/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-18.js
 * @description Array.prototype.indexOf - value of 'length' is a string that can't convert to a number
 */


function testcase() {

        var obj = { 0: true, 100: true, length: "one" };

        return Array.prototype.indexOf.call(obj, true) === -1;
    }
runTestCase(testcase);
