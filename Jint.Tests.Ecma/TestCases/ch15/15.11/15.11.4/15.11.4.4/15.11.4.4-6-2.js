/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-6-2.js
 * @description Error.prototype.toString - 'Error' is returned when 'name' is absent and value of 'msg' is returned when 'msg' is non-empty string
 */


function testcase() {
        var errObj = new Error("ErrorMessage");
        return errObj.toString() === "Error: ErrorMessage";
    }
runTestCase(testcase);
