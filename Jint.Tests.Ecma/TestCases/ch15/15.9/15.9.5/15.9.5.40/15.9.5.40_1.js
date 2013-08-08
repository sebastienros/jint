/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.40/15.9.5.40_1.js
 * @description Date.prototype.setFullYear - Date.prototype is itself an instance of Date
 */


function testcase() {
    try {
        var origYear = Date.prototype.getFullYear();
        Date.prototype.setFullYear(2012);
        return Date.prototype.getFullYear()===2012;
    } finally {
        Date.prototype.setFullYear(origYear);
    }
}
runTestCase(testcase);
