/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-b-3.js
 * @description Object.getOwnPropertyNames - own property named empty('') is pushed into the returned array
 */


function testcase() {
        var obj = { "": "empty" };

        var result = Object.getOwnPropertyNames(obj);

        for (var p in result) {
            if (result[p] === "") {
                return true;
            }
        }

        return false;
    }
runTestCase(testcase);
