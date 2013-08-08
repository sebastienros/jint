/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-9-1.js
 * @description Error.prototype.toString return 'name' when 'name' is non-empty string and 'msg' is empty string
 */


function testcase() {
        var errObj = new Error();
        errObj.name = "ErrorName";
        return errObj.toString() === "ErrorName";
    }
runTestCase(testcase);
