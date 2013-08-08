/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-10.js
 * @description Array.prototype.indexOf applied to the Math object
 */


function testcase() {
        try {
            Math[1] = true;
            Math.length = 2;
            return Array.prototype.indexOf.call(Math, true) === 1;
        } finally {
            delete Math[1];
            delete Math.length;
        }
    }
runTestCase(testcase);
