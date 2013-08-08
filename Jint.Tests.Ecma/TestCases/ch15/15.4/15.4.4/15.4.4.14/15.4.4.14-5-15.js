/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-15.js
 * @description Array.prototype.indexOf - value of 'fromIndex' is a string containing a negative number
 */


function testcase() {

        return [0, true, 2].indexOf(true, "-1") === -1 &&
        [0, 1, true].indexOf(true, "-1") === 2;
    }
runTestCase(testcase);
