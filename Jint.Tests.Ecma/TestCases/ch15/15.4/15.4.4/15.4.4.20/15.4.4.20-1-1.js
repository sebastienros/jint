/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-1.js
 * @description Array.prototype.filter applied to undefined throws a TypeError
 */


function testcase() {
        try {
            Array.prototype.filter.call(undefined); // TypeError is thrown if value is undefined
            return false;
        } catch (ex) {
            return ex instanceof TypeError;
        }
    }
runTestCase(testcase);
