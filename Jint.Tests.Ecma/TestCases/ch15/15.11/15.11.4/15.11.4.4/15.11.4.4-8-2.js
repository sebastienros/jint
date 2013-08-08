/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-8-2.js
 * @description Error.prototype.toString return empty string when 'name' is empty string and 'msg' is undefined
 */


function testcase() {
        var errObj = new Error();
        errObj.name = "";
        return errObj.toString() === "";
    }
runTestCase(testcase);
