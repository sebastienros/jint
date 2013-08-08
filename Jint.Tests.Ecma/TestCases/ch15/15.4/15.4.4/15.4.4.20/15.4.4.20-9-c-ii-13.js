/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-13.js
 * @description Array.prototype.filter - callbackfn that uses arguments object to get parameter value
 */


function testcase() {

        function callbackfn() {
            return arguments[2][arguments[1]] === arguments[0];
        }
        var newArr = [11].filter(callbackfn);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
