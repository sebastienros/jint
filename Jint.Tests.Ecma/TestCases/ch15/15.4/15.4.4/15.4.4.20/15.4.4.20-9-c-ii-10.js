/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-10.js
 * @description Array.prototype.filter - callbackfn is called with 1 formal parameter
 */


function testcase() {

        function callbackfn(val) {
            return val > 10;
        }
        var newArr = [12].filter(callbackfn);

        return newArr.length === 1 && newArr[0] === 12;
    }
runTestCase(testcase);
