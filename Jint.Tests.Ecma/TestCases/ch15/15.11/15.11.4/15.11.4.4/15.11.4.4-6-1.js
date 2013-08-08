/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-6-1.js
 * @description Error.prototype.toString - 'Error' is returned when 'name' is absent and empty string is returned when 'msg' is undefined
 */


function testcase() {
        var errObj = new Error();
        return errObj.toString() === "Error";
    }
runTestCase(testcase);
