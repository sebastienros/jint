/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-10.js
 * @description Array.prototype.reduceRight - callbackfn is called with 1 formal parameter
 */


function testcase() {

        var called = 0;

        function callbackfn(prevVal) {
            called++;
            return prevVal;
        }

        return [11, 12].reduceRight(callbackfn, 100) === 100 && 2 === called;
    }
runTestCase(testcase);
