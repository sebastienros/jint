/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-1.js
 * @description Array.prototype.indexOf returns -1 when 'fromIndex' is length of array - 1
 */


function testcase() {

        return [1, 2, 3].indexOf(1, 2) === -1;
    }
runTestCase(testcase);
