/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-6-3.js
 * @description Object.keys - the order of elements in returned array is the same with the order of properties in 'O' (String object)
 */


function testcase() {
        var str = new String("abc");

        var tempArray = [];
        for (var p in str) {
            if (str.hasOwnProperty(p)) {
                tempArray.push(p);
            }
        }

        var returnedArray = Object.keys(str);

        for (var index in returnedArray) {
            if (tempArray[index] !== returnedArray[index]) {
                return false;
            }
        }
        return true;
    }
runTestCase(testcase);
