/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-5.js
 * @description Object.getOwnPropertyNames - elements of the returned array are enumerable
 */


function testcase() {
        var obj = { "a": "a" };

        var result = Object.getOwnPropertyNames(obj);

        for (var p in result) {
            if (result[p] === "a") {
                return true;
            }
        }

        return false;
    }
runTestCase(testcase);
