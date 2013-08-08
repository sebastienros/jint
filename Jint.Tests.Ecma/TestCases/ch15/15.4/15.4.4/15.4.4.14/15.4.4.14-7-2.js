/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-2.js
 * @description Array.prototype.indexOf returns correct index when 'fromIndex' is length of array - 1
 */


function testcase() {

        return [1, 2, 3].indexOf(3, 2) === 2;
    }
runTestCase(testcase);
