/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-4.js
 * @description Array.prototype.indexOf - search element is NaN
 */


function testcase() {

        return [+NaN, NaN, -NaN].indexOf(NaN) === -1;
    }
runTestCase(testcase);
