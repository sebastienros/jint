/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-7-3.js
 * @description Array.prototype.indexOf returns -1 when 'fromIndex' and 'length' are both 0
 */


function testcase() {

        return [].indexOf(1, 0) === -1;
    }
runTestCase(testcase);
