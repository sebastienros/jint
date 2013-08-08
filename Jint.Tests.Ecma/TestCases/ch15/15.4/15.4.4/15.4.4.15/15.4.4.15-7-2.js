/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-7-2.js
 * @description Array.prototype.lastIndexOf returns correct index when 'fromIndex' is -1
 */


function testcase() {

        return [1, 2, 3, 4].lastIndexOf(4, -1) === 3;
    }
runTestCase(testcase);
