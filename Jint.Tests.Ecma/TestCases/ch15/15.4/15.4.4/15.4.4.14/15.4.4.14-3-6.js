/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-6.js
 * @description Array.prototype.indexOf - value of 'length' is a number (value is positive)
 */


function testcase() {

        var obj = { 3: true, 4: false, length: 4 };

        return Array.prototype.indexOf.call(obj, true) === 3 &&
            Array.prototype.indexOf.call(obj, false) === -1;
    }
runTestCase(testcase);
