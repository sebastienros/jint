/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-2.js
 * @description Array.prototype.filter - 'length' is own data property on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return obj.length === 2;
        }

        var newArr = [12, 11].filter(callbackfn);
        return newArr.length === 2;
    }
runTestCase(testcase);
