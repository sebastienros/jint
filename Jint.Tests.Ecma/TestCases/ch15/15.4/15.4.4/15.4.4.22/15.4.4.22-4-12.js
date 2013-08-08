/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-12.js
 * @description Array.prototype.reduceRight - 'callbackfn' is a function
 */


function testcase() {

        var initialValue = 0;
        function callbackfn(accum, val, idx, obj) {
            accum += val;
            return accum;
        }

        return 20 === [11, 9].reduceRight(callbackfn, initialValue);
    }
runTestCase(testcase);
