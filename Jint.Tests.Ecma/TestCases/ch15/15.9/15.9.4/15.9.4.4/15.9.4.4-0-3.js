/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.4/15.9.4.4/15.9.4.4-0-3.js
 * @description Date.now must exist as a function
 */


function testcase() {

        var fun = Date.now;
        return (typeof (fun) === "function");
    }
runTestCase(testcase);
