/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-5.js
 * @description Array.prototype.lastIndexOf applied to number primitive
 */


function testcase() {

        try {
                Number.prototype[1] = isNaN;
                Number.prototype.length = 2;
                return 1 === Array.prototype.lastIndexOf.call(5, isNaN);
            } finally {
                delete Number.prototype[1];
                delete Number.prototype.length;
            }
    }
runTestCase(testcase);
