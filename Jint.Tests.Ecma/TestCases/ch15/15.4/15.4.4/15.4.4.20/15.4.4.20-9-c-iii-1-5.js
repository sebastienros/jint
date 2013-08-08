/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-5.js
 * @description Array.prototype.filter - values of 'to' are passed in acending numeric order
 */


function testcase() {

        var arr = [0, 1, 2, 3, 4];
        var lastToIdx = 0;
        var called = 0;
        function callbackfn(val, idx, obj) {
            called++;
            if (lastToIdx !== idx) {
                return false;
            } else {
                lastToIdx++;
                return true;
            }
        }
        var newArr = arr.filter(callbackfn);

        return newArr.length === 5 && called === 5;
    }
runTestCase(testcase);
