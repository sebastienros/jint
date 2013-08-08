/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-10.js
 * @description Array.prototype.filter - Array Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objArray = new Array(10);

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objArray;
        }


        var newArr = [11].filter(callbackfn, objArray);

        return newArr[0] === 11 && accessed;
    }
runTestCase(testcase);
