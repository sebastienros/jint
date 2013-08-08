/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-17.js
 * @description Array.prototype.filter return value of callbackfn is an Array object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return new Array(10);
        }

        var newArr = [11].filter(callbackfn);
        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
