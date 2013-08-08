/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-1.js
 * @description Array.prototype.filter doesn't consider new elements added to array after it is called
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            srcArr[2] = 3;
            srcArr[5] = 6;
            return true;
        }

        var srcArr = [1, 2, , 4, 5];
        var resArr = srcArr.filter(callbackfn);
        return resArr.length === 5;

    }
runTestCase(testcase);
