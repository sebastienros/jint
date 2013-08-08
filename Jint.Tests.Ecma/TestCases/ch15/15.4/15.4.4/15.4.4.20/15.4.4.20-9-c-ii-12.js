/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-12.js
 * @description Array.prototype.filter - callbackfn is called with 3 formal parameter
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val > 10 && obj[idx] === val;
        }
        var newArr = [11].filter(callbackfn);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
