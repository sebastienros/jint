/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.4/15.9.4.4/15.9.4.4-0-4.js
 * @description Date.now - returns number
 */


function testcase() {        
        return typeof Date.now() === "number";
    }
runTestCase(testcase);
