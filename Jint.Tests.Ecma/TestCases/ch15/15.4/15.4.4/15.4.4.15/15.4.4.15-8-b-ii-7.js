/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-7.js
 * @description Array.prototype.lastIndexOf - array element is -0 and search element is +0
 */


function testcase() {

        return [-0].lastIndexOf(+0) === 0;
    }
runTestCase(testcase);
