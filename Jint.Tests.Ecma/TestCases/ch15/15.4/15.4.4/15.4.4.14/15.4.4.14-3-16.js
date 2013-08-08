/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-16.js
 * @description Array.prototype.indexOf - 'length' is a string containing a hex number
 */


function testcase() {

        var obj = { 10: true, 11: "0x00B", length: "0x00B" };

        return Array.prototype.indexOf.call(obj, true) === 10 &&
            Array.prototype.indexOf.call(obj, "0x00B") === -1;
    }
runTestCase(testcase);
