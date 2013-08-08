/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-1.js
 * @description Array.prototype.lastIndexOf - undefined property wouldn't be called
 */


function testcase() {

        return [0, , 2].lastIndexOf(undefined) === -1;
    }
runTestCase(testcase);
