/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-34.js
 * @description String.prototype.trim - 'this' is an array that converts to a string
 */


function testcase() {
        return (String.prototype.trim.call([1]) === '1');
    }
runTestCase(testcase);
