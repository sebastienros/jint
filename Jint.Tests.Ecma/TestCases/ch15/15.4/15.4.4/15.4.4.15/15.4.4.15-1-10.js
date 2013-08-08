/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-10.js
 * @description Array.prototype.lastIndexOf applied to the Math object
 */


function testcase() {
    
        try {
            Math.length = 2;
            Math[1] = 100;
            return 1 === Array.prototype.lastIndexOf.call(Math, 100);
        } finally {
            delete Math.length;
            delete Math[1];
        }
    }
runTestCase(testcase);
