/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-iii-1.js
 * @description Array.prototype.indexOf - returns index of last one when more than two elements in array are eligible
 */


function testcase() {

        return [1, 2, 2, 1, 2].indexOf(2) === 1;
    }
runTestCase(testcase);
