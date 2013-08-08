/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-4.js
 * @description Array.prototype.lastIndexOf returns -1 when 'fromIndex' and 'length' are both 0
 */


function testcase() {

        return [].lastIndexOf(1, 0) === -1;
    }
runTestCase(testcase);
