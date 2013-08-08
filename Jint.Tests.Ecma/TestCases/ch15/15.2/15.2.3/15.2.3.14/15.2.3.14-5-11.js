/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-11.js
 * @description Object.keys - own enumerable indexed data property of dense array 'O' is defined in returned array
 */


function testcase() {
        var obj = [1, 2, 3, 4, 5];

        var arr = Object.keys(obj);

        var initValue = 0;
        for (var p in arr) {
            if (arr.hasOwnProperty(p)) {
                if (arr[p] !== initValue.toString()) {
                    return false;
                }
                initValue++;
            }
        }

        return true;
    }
runTestCase(testcase);
