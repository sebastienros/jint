/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-8-1.js
 * @description Error.prototype.toString return the value of 'msg' when 'name' is empty string and 'msg' isn't undefined
 */


function testcase() {
        var errObj = new Error("ErrorMessage");
        errObj.name = "";
        return errObj.toString() === "ErrorMessage";
    }
runTestCase(testcase);
