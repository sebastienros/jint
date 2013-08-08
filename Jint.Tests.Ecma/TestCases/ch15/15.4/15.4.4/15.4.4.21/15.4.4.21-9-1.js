/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-1.js
 * @description Array.prototype.reduce doesn't consider new elements added to array after it is called
 */


function testcase() {
        function callbackfn(prevVal, curVal, idx, obj) {
            arr[5] = 6;
            arr[2] = 3;
            return prevVal + curVal;
        }

        var arr = [1, 2, , 4, '5'];
        return arr.reduce(callbackfn) === "105";
    }
runTestCase(testcase);
