/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-15.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is a string containing a negative number
 */


function testcase() {

        return [0, "-2", 2].lastIndexOf("-2", "-2") === 1 &&
            [0, 2, "-2"].lastIndexOf("-2", "-2") === -1;
    }
runTestCase(testcase);
