/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-10-1.js
 * @description Error.prototype.toString return the result of concatenating 'name', ':', a single space character, and 'msg' when 'name' and 'msg' are non-empty string
 */


function testcase() {
        var errObj = new Error("ErrorMessage");
        errObj.name = "ErrorName";
        return errObj.toString() === "ErrorName: ErrorMessage";
    }
runTestCase(testcase);
