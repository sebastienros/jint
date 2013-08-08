/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-5.js
 * @description Array.prototype.reduceRight - k values are accessed during each iteration and not prior to starting the loop on an Array
 */


function testcase() {
        var arr = [11, 12, 13, 14];
        var kIndex = [];
        var result = true;
        var called = 0;

        //By below way, we could verify that k would be setted as 0, 1, ..., length - 1 in order, and each value will be setted one time.
        function callbackfn(preVal, curVal, idx, o) {
            //Each position should be visited one time, which means k is accessed one time during iterations.
            called++;
            if (typeof kIndex[idx] === "undefined") {
                //when current position is visited, its next index should has been visited.
                if (idx !== arr.length - 1 && typeof kIndex[idx + 1] === "undefined") {
                    result = false;
                }
                kIndex[idx] = 1;
            } else {
                result = false;
            }
        }

        arr.reduceRight(callbackfn, 1);
        return result && called === 4;
    }
runTestCase(testcase);
