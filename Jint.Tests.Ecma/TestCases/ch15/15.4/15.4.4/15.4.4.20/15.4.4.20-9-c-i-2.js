/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-2.js
 * @description Array.prototype.filter - element to be retrieved is own data property on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === 11;
            }
        }

        var newArr = [11].filter(callbackfn);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
