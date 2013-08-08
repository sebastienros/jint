/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-5.js
 * @description Array.prototype.indexOf returns correct index when 'fromIndex' is 1
 */


function testcase() {

        return [1, 2, 3].indexOf(2, 1) === 1;
    }
runTestCase(testcase);
