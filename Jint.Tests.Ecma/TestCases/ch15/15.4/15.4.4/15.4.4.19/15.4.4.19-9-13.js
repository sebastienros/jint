/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-13.js
 * @description Array.prototype.map - if there are no side effects of the functions, O is unmodified
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return val > 2;
        }

        var arr = [1, 2, 3, 4];

        arr.map(callbackfn);

        return 1 === arr[0] && 2 === arr[1] && 3 === arr[2] && 4 === arr[3] && 4 === called;


    }
runTestCase(testcase);
