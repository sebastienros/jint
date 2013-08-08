/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-10.js
 * @description Array.prototype.indexOf - value of 'length' is number primitive (value is NaN)
 */


function testcase() {

        var obj = { 0: 0, length: NaN };

        return Array.prototype.indexOf.call(obj, 0) === -1;
    }
runTestCase(testcase);
